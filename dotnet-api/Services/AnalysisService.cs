using dotnet_api.Models.DTOs;
using dotnet_api.Models;
using dotnet_api.Data;
using dotnet_api.Services.Helpers;
using Microsoft.EntityFrameworkCore;
using Octokit;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace dotnet_api.Services;

public interface IAnalysisService
{
    Task<AnalysisResultDto> AnalyzeBranchAsync(AnalyzeBranchDto request, int userId);
    Task<int> StartAnalysisAsync(AnalyzeBranchDto request, int userId, string correlationId);
    Task ProcessAnalysisAsync(int runId, AnalyzeBranchDto request, int userId, string correlationId, CancellationToken cancellationToken = default);
    Task<List<string>> GetRemoteBranchesAsync(string repoUrl, string? githubToken = null);
    Task<List<AnalysisRunListItemDto>> GetAnalysisRunsAsync(int userId, string? repoUrl = null, string? branchName = null, int limit = 20);
    Task<AnalysisRunDetailDto?> GetAnalysisRunByIdAsync(int runId, int userId);
    Task<List<IssueDto>> GetAnalysisRunIssuesAsync(int runId, int userId, string? severity = null, string? category = null);
}

public class AnalysisService : IAnalysisService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AnalysisService> _logger;
    private readonly IMetricsService? _metricsService;
    
    // Helper services
    private readonly IGitHubClientService _githubClientService;
    private readonly IPhpServiceClient _phpServiceClient;
    private readonly IDataSanitizer _dataSanitizer;
    private readonly IFileFilter _fileFilter;
    private readonly IGitHubFileService _githubFileService;
    private readonly IRepositoryService _repositoryService;
    private readonly IMetricsCalculator _metricsCalculator;
    private readonly IMemoryCache _cache;

    public AnalysisService(
        ApplicationDbContext context,
        ILogger<AnalysisService> logger,
        IGitHubClientService githubClientService,
        IPhpServiceClient phpServiceClient,
        IDataSanitizer dataSanitizer,
        IFileFilter fileFilter,
        IGitHubFileService githubFileService,
        IRepositoryService repositoryService,
        IMetricsCalculator metricsCalculator,
        IMemoryCache cache,
        IMetricsService? metricsService = null)
    {
        _context = context;
        _logger = logger;
        _githubClientService = githubClientService;
        _phpServiceClient = phpServiceClient;
        _dataSanitizer = dataSanitizer;
        _fileFilter = fileFilter;
        _githubFileService = githubFileService;
        _repositoryService = repositoryService;
        _metricsCalculator = metricsCalculator;
        _cache = cache;
        _metricsService = metricsService;
    }

    public async Task<AnalysisResultDto> AnalyzeBranchAsync(AnalyzeBranchDto request, int userId)
    {
        var correlationId = Guid.NewGuid().ToString("N")[..8];
        var startTime = DateTime.UtcNow;
        
        _logger.LogInformation(
            "[{CorrelationId}] Starting branch analysis. UserId: {UserId}, RepoUrl: {RepoUrl}, Branch: {BranchName}",
            correlationId, userId, _dataSanitizer.RedactSensitiveData(request.RepoUrl), request.BranchName);

        AnalysisRun? run = null;
        dotnet_api.Models.Repository? repository = null;

        try
        {
            // Get authenticated GitHub client
            var authenticatedClient = _githubClientService.GetAuthenticatedClient(request.GithubToken);
            
            // Parse repo URL
            var (owner, repoName) = _githubClientService.ParseRepoUrl(request.RepoUrl);
            _logger.LogDebug("[{CorrelationId}] Parsed repository: {Owner}/{RepoName}", correlationId, owner, repoName);

            // Get or create repository
            repository = await _repositoryService.GetOrCreateRepositoryAsync(userId, request.RepoUrl, request.GithubToken, correlationId);

            // Get repository info from GitHub
            var githubRepo = await authenticatedClient.Repository.Get(owner, repoName);
            var defaultBranch = githubRepo.DefaultBranch;

            // Get commit SHAs
            var defaultBranchRef = await authenticatedClient.Git.Reference.Get(owner, repoName, $"heads/{defaultBranch}");
            var branchRef = await authenticatedClient.Git.Reference.Get(owner, repoName, $"heads/{request.BranchName}");
            
            var baseCommitSha = defaultBranchRef.Object.Sha;
            var headCommitSha = branchRef.Object.Sha;

            _logger.LogDebug(
                "[{CorrelationId}] Commit SHAs resolved. Base: {BaseSha}, Head: {HeadSha}, DefaultBranch: {DefaultBranch}",
                correlationId, baseCommitSha[..8], headCommitSha[..8], defaultBranch);

            // Check for existing run (idempotency)
            var existingRun = await _context.AnalysisRuns
                .FirstOrDefaultAsync(r => r.RepositoryId == repository.Id 
                    && r.BaseCommitSha == baseCommitSha 
                    && r.HeadCommitSha == headCommitSha);

            if (existingRun != null && existingRun.Status == "completed")
            {
                _logger.LogInformation("[{CorrelationId}] Found existing completed run: {RunId}, returning cached result", correlationId, existingRun.Id);
                return await BuildResultFromRunAsync(existingRun);
            }

            // Create analysis run
            run = new AnalysisRun
            {
                RepositoryId = repository.Id,
                BranchName = request.BranchName,
                DefaultBranch = defaultBranch,
                BaseCommitSha = baseCommitSha,
                HeadCommitSha = headCommitSha,
                Status = "running",
                FilesAnalyzed = 0,
                AverageScore = 0,
                TotalIssues = 0,
                CreatedAt = DateTime.UtcNow
            };

            _context.AnalysisRuns.Add(run);
            await _context.SaveChangesAsync();
            _logger.LogInformation("[{CorrelationId}] Created analysis run: {RunId}", correlationId, run.Id);

            // Fetch code files
            var branchFiles = await _githubFileService.GetCodeFromBranchAsync(
                owner, repoName, request.BranchName, defaultBranch, correlationId, authenticatedClient);

            if (branchFiles.Count == 0)
            {
                run.Status = "completed";
                run.Summary = "No files found for analysis.";
                run.CompletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return new AnalysisResultDto
                {
                    RunId = run.Id.ToString(),
                    RepoName = $"{owner}/{repoName}",
                    BranchName = request.BranchName,
                    CommitSha = headCommitSha,
                    FilesAnalyzed = 0,
                    AverageScore = 0,
                    TotalIssues = 0,
                    AnalyzedAt = DateTime.UtcNow
                };
            }

            _logger.LogDebug("[{CorrelationId}] Found {FileCount} files to analyze", correlationId, branchFiles.Count);

            // Apply file count limit
            branchFiles = _fileFilter.ApplyFileCountLimit(branchFiles, correlationId, _logger);

            // Analyze with PHP service
            var phpResult = await _phpServiceClient.AnalyzeFilesAsync(branchFiles, correlationId);

            // Calculate metrics
            var averageScore = phpResult.AverageScore;
            var totalIssues = phpResult.TotalIssues;
            var issuesBySeverity = _metricsCalculator.CalculateIssuesBySeverity(phpResult.Results);
            var fileMetrics = _metricsCalculator.CalculateFileMetrics(phpResult.Results);

            // Save issues and metrics
            await _metricsCalculator.SaveIssuesAndMetricsAsync(run.Id, phpResult, branchFiles, headCommitSha, correlationId);

            // Update run
            run.Status = "completed";
            run.FilesAnalyzed = phpResult.FilesAnalyzed;
            run.AverageScore = (decimal)averageScore;
            run.TotalIssues = totalIssues;
            run.Summary = $"Analyzed {phpResult.FilesAnalyzed} files. Score: {averageScore:F2}/100. Issues: {totalIssues}";
            
            // Compress RawOutput to reduce storage
            var rawOutputJson = JsonSerializer.Serialize(phpResult);
            run.RawOutput = _dataSanitizer.CompressString(rawOutputJson);
            
            run.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var duration = (DateTime.UtcNow - startTime).TotalSeconds;
            
            // Record metrics
            _metricsService?.RecordAnalysisDuration(duration, phpResult.FilesAnalyzed, totalIssues);
            foreach (var fileResult in phpResult.Results)
            {
                foreach (var issue in fileResult.Issues)
                {
                    _metricsService?.RecordIssueDistribution(issue.Severity, issue.Category);
                }
            }
            
            _logger.LogInformation(
                "[{CorrelationId}] Analysis complete. RunId: {RunId}, Score: {Score}, Files: {Files}, Issues: {Issues}, Duration: {Duration}s",
                correlationId, run.Id, averageScore, phpResult.FilesAnalyzed, totalIssues, duration);

            // Build and return result
            return new AnalysisResultDto
            {
                RunId = run.Id.ToString(),
                RepoName = $"{owner}/{repoName}",
                BranchName = request.BranchName,
                CommitSha = headCommitSha,
                FilesAnalyzed = phpResult.FilesAnalyzed,
                AverageScore = averageScore,
                TotalIssues = totalIssues,
                IssuesBySeverity = issuesBySeverity,
                FileMetrics = fileMetrics,
                AnalyzedAt = DateTime.UtcNow
            };
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "[{CorrelationId}] GitHub resource not found", correlationId);
            if (run != null)
            {
                run.Status = "failed";
                run.Summary = $"Repository or branch not found: {ex.Message}";
                run.CompletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            throw;
        }
        catch (RateLimitExceededException ex)
        {
            _logger.LogWarning(ex, "[{CorrelationId}] GitHub rate limit exceeded", correlationId);
            if (run != null)
            {
                run.Status = "failed";
                run.Summary = "GitHub API rate limit exceeded";
                run.CompletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{CorrelationId}] Unexpected error during analysis", correlationId);
            if (run != null)
            {
                run.Status = "failed";
                run.Summary = $"Analysis failed: {_dataSanitizer.RedactSensitiveData(ex.Message)}";
                run.CompletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            throw;
        }
    }

    public async Task<int> StartAnalysisAsync(AnalyzeBranchDto request, int userId, string correlationId)
    {
        _logger.LogInformation(
            "[{CorrelationId}] Starting async branch analysis. UserId: {UserId}, RepoUrl: {RepoUrl}, Branch: {BranchName}",
            correlationId, userId, _dataSanitizer.RedactSensitiveData(request.RepoUrl), request.BranchName);

        try
        {
            // Get authenticated GitHub client
            var authenticatedClient = _githubClientService.GetAuthenticatedClient(request.GithubToken);
            
            // Parse repo URL and get/create repository
            var (owner, repoName) = _githubClientService.ParseRepoUrl(request.RepoUrl);
            _logger.LogInformation("[{CorrelationId}] Parsed repository: {Owner}/{RepoName}", correlationId, owner, repoName);

            // Get or create repository
            var repository = await _repositoryService.GetOrCreateRepositoryAsync(userId, request.RepoUrl, request.GithubToken, correlationId);

            // Get repository info from GitHub
            var githubRepo = await authenticatedClient.Repository.Get(owner, repoName);
            var defaultBranch = githubRepo.DefaultBranch;

            // Get commit SHAs
            var defaultBranchRef = await authenticatedClient.Git.Reference.Get(owner, repoName, $"heads/{defaultBranch}");
            var branchRef = await authenticatedClient.Git.Reference.Get(owner, repoName, $"heads/{request.BranchName}");
            
            var baseCommitSha = defaultBranchRef.Object.Sha;
            var headCommitSha = branchRef.Object.Sha;

            _logger.LogInformation(
                "[{CorrelationId}] Commit SHAs resolved. Base: {BaseSha}, Head: {HeadSha}, DefaultBranch: {DefaultBranch}",
                correlationId, baseCommitSha[..8], headCommitSha[..8], defaultBranch);

            // Check for existing run (idempotency)
            var existingRun = await _context.AnalysisRuns
                .FirstOrDefaultAsync(r => r.RepositoryId == repository.Id 
                    && r.BaseCommitSha == baseCommitSha 
                    && r.HeadCommitSha == headCommitSha);

            if (existingRun != null)
            {
                if (existingRun.Status == "completed")
                {
                    // Don't reuse runs with 0 files - force re-analysis
                    if (existingRun.FilesAnalyzed == 0)
                    {
                        _logger.LogWarning("[{CorrelationId}] Found existing completed run {RunId} with 0 files. Creating new run to re-analyze.", correlationId, existingRun.Id);
                    }
                    else
                    {
                        _logger.LogInformation("[{CorrelationId}] Found existing completed run: {RunId} with {FileCount} files", correlationId, existingRun.Id, existingRun.FilesAnalyzed);
                        return existingRun.Id;
                    }
                }
                else if (existingRun.Status == "running" || existingRun.Status == "queued")
                {
                    _logger.LogInformation("[{CorrelationId}] Found existing {Status} run: {RunId}. Returning existing ID instead of creating duplicate.", correlationId, existingRun.Status, existingRun.Id);
                    return existingRun.Id;
                }
            }

            // Create analysis run
            var run = new AnalysisRun
            {
                RepositoryId = repository.Id,
                BranchName = request.BranchName,
                DefaultBranch = defaultBranch,
                BaseCommitSha = baseCommitSha,
                HeadCommitSha = headCommitSha,
                Status = "running",
                FilesAnalyzed = 0,
                AverageScore = 0,
                TotalIssues = 0,
                CreatedAt = DateTime.UtcNow
            };

            _context.AnalysisRuns.Add(run);
            await _context.SaveChangesAsync();
            _logger.LogInformation("[{CorrelationId}] Created analysis run: {RunId}", correlationId, run.Id);

            return run.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{CorrelationId}] Failed to start analysis", correlationId);
            throw;
        }
    }

    public async Task ProcessAnalysisAsync(int runId, AnalyzeBranchDto request, int userId, string correlationId, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        AnalysisRun? run = null;

        try
        {
            run = await _context.AnalysisRuns.FindAsync(new object[] { runId }, cancellationToken);
            if (run == null)
            {
                _logger.LogError("[{CorrelationId}] Analysis run {RunId} not found", correlationId, runId);
                throw new InvalidOperationException($"Analysis run {runId} not found");
            }

            if (run.Status != "running")
            {
                _logger.LogWarning("[{CorrelationId}] Analysis run {RunId} is not in running state: {Status}", correlationId, runId, run.Status);
                return;
            }

            _logger.LogInformation("[{CorrelationId}] Processing analysis run {RunId}", correlationId, runId);

            // Get authenticated GitHub client
            var authenticatedClient = _githubClientService.GetAuthenticatedClient(request.GithubToken);
            
            // Get repository
            var repository = await _context.Repositories.FindAsync(new object[] { run.RepositoryId }, cancellationToken);
            if (repository == null)
            {
                throw new InvalidOperationException($"Repository {run.RepositoryId} not found");
            }

            var (owner, repoName) = _githubClientService.ParseRepoUrl(repository.CloneUrl);

            // Fetch code files
            _logger.LogInformation("[{CorrelationId}] Starting file fetch. RunId: {RunId}, Branch: {BranchName}, DefaultBranch: {DefaultBranch}, Owner: {Owner}, Repo: {RepoName}", 
                correlationId, runId, request.BranchName, run.DefaultBranch, owner, repoName);
            
            var branchFiles = await _githubFileService.GetCodeFromBranchAsync(
                owner, repoName, request.BranchName, run.DefaultBranch, correlationId, authenticatedClient);

            _logger.LogInformation("[{CorrelationId}] File fetch completed. Found {FileCount} files for analysis", correlationId, branchFiles.Count);

            if (branchFiles.Count == 0)
            {
                _logger.LogWarning("[{CorrelationId}] No files found for analysis. RunId: {RunId}, Branch: {BranchName}, DefaultBranch: {DefaultBranch}", 
                    correlationId, runId, request.BranchName, run.DefaultBranch);
                run.Status = "completed";
                run.Summary = "No files found for analysis. This may occur if: 1) Branch has no changes compared to default, 2) All files were filtered out, 3) Branch is empty.";
                run.CompletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("[{CorrelationId}] Marked run {RunId} as completed with 0 files", correlationId, runId);
                return;
            }

            _logger.LogInformation("[{CorrelationId}] Found {FileCount} files to analyze", correlationId, branchFiles.Count);

            // Apply file count limit
            branchFiles = _fileFilter.ApplyFileCountLimit(branchFiles, correlationId, _logger);

            // Analyze with PHP service
            var phpResult = await _phpServiceClient.AnalyzeFilesAsync(branchFiles, correlationId);

            // Calculate metrics
            var averageScore = phpResult.AverageScore;
            var totalIssues = phpResult.TotalIssues;

            // Save issues and metrics with explicit error handling
            try
            {
                _logger.LogInformation("[{CorrelationId}] Starting to save issues and metrics for run {RunId}", correlationId, run.Id);
                await _metricsCalculator.SaveIssuesAndMetricsAsync(run.Id, phpResult, branchFiles, run.HeadCommitSha, correlationId);
                _logger.LogInformation("[{CorrelationId}] Successfully saved issues and metrics for run {RunId}", correlationId, run.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{CorrelationId}] CRITICAL: Failed to save issues and metrics for run {RunId}. This will cause transaction rollback.", correlationId, run.Id);
                throw; // Re-throw to ensure the transaction fails properly
            }

            // Update run status
            _logger.LogInformation("[{CorrelationId}] Updating run {RunId} status to completed", correlationId, run.Id);
            run.Status = "completed";
            run.FilesAnalyzed = phpResult.FilesAnalyzed;
            run.AverageScore = (decimal)averageScore;
            run.TotalIssues = totalIssues;
            run.Summary = $"Analyzed {phpResult.FilesAnalyzed} files. Score: {averageScore:F2}/100. Issues: {totalIssues}";
            
            // Compress RawOutput to reduce storage
            var rawOutputJson = JsonSerializer.Serialize(phpResult);
            run.RawOutput = _dataSanitizer.CompressString(rawOutputJson);
            
            run.CompletedAt = DateTime.UtcNow;
            
            try
            {
                // FORCE TRACKING: Explicitly tell EF Core this entity has changed
                // This ensures the UPDATE command is sent even if the tracker lost track of the status change
                _context.Entry(run).State = EntityState.Modified;
                
                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("[{CorrelationId}] Successfully updated run {RunId} status to completed. Verified in tracking state.", correlationId, run.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{CorrelationId}] CRITICAL: Failed to save run {RunId} completion status", correlationId, run.Id);
                throw;
            }

            var duration = (DateTime.UtcNow - startTime).TotalSeconds;
            
            // Record metrics
            _metricsService?.RecordAnalysisDuration(duration, phpResult.FilesAnalyzed, totalIssues);
            foreach (var fileResult in phpResult.Results)
            {
                foreach (var issue in fileResult.Issues)
                {
                    _metricsService?.RecordIssueDistribution(issue.Severity, issue.Category);
                }
            }
            
            _logger.LogInformation(
                "[{CorrelationId}] Analysis complete. RunId: {RunId}, Score: {Score}, Files: {Files}, Issues: {Issues}, Duration: {Duration}s",
                correlationId, run.Id, averageScore, phpResult.FilesAnalyzed, totalIssues, duration);
        }
        catch (RateLimitExceededException ex)
        {
            _logger.LogWarning(ex, "[{CorrelationId}] GitHub rate limit exceeded during processing", correlationId);
            if (run != null)
            {
                run.Status = "failed";
                run.Summary = "GitHub API rate limit exceeded. Please provide a GitHub token or wait before retrying.";
                run.CompletedAt = DateTime.UtcNow;
                try { await _context.SaveChangesAsync(cancellationToken); } catch { }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{CorrelationId}] Error processing analysis run {RunId}", correlationId, runId);
            
            if (run != null)
            {
                run.Status = "failed";
                run.Summary = $"Analysis failed: {ex.Message}";
                run.CompletedAt = DateTime.UtcNow;
                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                }
                catch (Exception saveEx)
                {
                    _logger.LogError(saveEx, "[{CorrelationId}] Failed to save error status for run {RunId}", correlationId, runId);
                }
            }
            throw;
        }
    }

    public async Task<List<string>> GetRemoteBranchesAsync(string repoUrl, string? githubToken = null)
    {
        var branches = await _githubClientService.GetRemoteBranchesAsync(repoUrl, githubToken);
        return branches.ToList();
    }

    public async Task<List<AnalysisRunListItemDto>> GetAnalysisRunsAsync(int userId, string? repoUrl = null, string? branchName = null, int limit = 20)
    {
        // Clear change tracker for fresh data
        _context.ChangeTracker.Clear();
        
        var query = _context.AnalysisRuns
            .AsNoTracking() // ⚡ KEY FIX - Don't use cached entities
            .Include(r => r.Repository)
            .Where(r => r.Repository.UserId == userId);

        if (!string.IsNullOrEmpty(repoUrl))
        {
            var sanitizedUrl = _githubClientService.SanitizeRepoUrl(repoUrl);
            query = query.Where(r => r.Repository.CloneUrl.Contains(sanitizedUrl));
        }

        if (!string.IsNullOrEmpty(branchName))
        {
            query = query.Where(r => r.BranchName == branchName);
        }

        var runs = await query
            .OrderByDescending(r => r.CreatedAt)
            .Take(limit)
            .ToListAsync();

        return runs.Select(r => new AnalysisRunListItemDto
        {
            Id = r.Id,
            RepoName = r.Repository.FullName ?? r.Repository.Name,
            BranchName = r.BranchName,
            Status = r.Status,
            FilesAnalyzed = r.FilesAnalyzed,
            AverageScore = (double)r.AverageScore,
            TotalIssues = r.TotalIssues,
            CreatedAt = r.CreatedAt,
            CompletedAt = r.CompletedAt
        }).ToList();
    }

    public async Task<AnalysisRunDetailDto?> GetAnalysisRunByIdAsync(int runId, int userId)
    {
        string cacheKey = $"analysis_run_{runId}_{userId}";

        // Try to get from cache first
        if (_cache.TryGetValue(cacheKey, out AnalysisRunDetailDto? cachedResult))
        {
            _logger.LogDebug("Returning cached results for run {RunId}", runId);
            return cachedResult;
        }

        // Use AsNoTracking() to bypass EF Core local cache and always fetch fresh from database
        var run = await _context.AnalysisRuns
            .AsNoTracking() 
            .Include(r => r.Repository)
            .Include(r => r.Issues)
            .FirstOrDefaultAsync(r => r.Id == runId && r.Repository.UserId == userId);

        if (run == null)
        {
            return null;
        }

        // ⚡ OPTIMIZATION: If analysis is still in progress (polling), return basic detail immediately
        // DO NOT cache this, as the status will change soon!
        if (run.Status == "running" || run.Status == "queued")
        {
            return new AnalysisRunDetailDto
            {
                Id = run.Id,
                RepoName = run.Repository.FullName ?? run.Repository.Name,
                BranchName = run.BranchName,
                Status = run.Status,
                FilesAnalyzed = run.FilesAnalyzed,
                CreatedAt = run.CreatedAt
            };
        }

        var fileContents = new Dictionary<string, string>();
        var fileMetrics = new Dictionary<string, FileMetricsDto>();

        // Get file metrics from AnalysisMetrics - also use AsNoTracking()
        var metrics = await _context.AnalysisMetrics
            .AsNoTracking()
            .Where(m => m.RunId == runId)
            .FirstOrDefaultAsync();

        if (metrics != null && !string.IsNullOrEmpty(metrics.MetricsJson))
        {
            try
            {
                fileMetrics = JsonSerializer.Deserialize<Dictionary<string, FileMetricsDto>>(metrics.MetricsJson) ?? new Dictionary<string, FileMetricsDto>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize file metrics for run {RunId}", runId);
            }
        }

        // Fetch file contents from GitHub if available
        if (!string.IsNullOrEmpty(run.Repository.CloneUrl) && !string.IsNullOrEmpty(run.HeadCommitSha) && run.Issues.Any())
        {
            try
            {
                var (owner, repoName) = _githubClientService.ParseRepoUrl(run.Repository.CloneUrl);
                var client = _githubClientService.GetAuthenticatedClient(null);

                // Get distinct file paths to fetch
                var filesToFetch = run.Issues.Select(i => i.FilePath).Distinct().ToList();
                _logger.LogDebug("[{RunId}] Parallel fetching contents for {Count} files", runId, filesToFetch.Count);

                var tasks = filesToFetch.Select(async filePath =>
                {
                    try
                    {
                        var contents = await client.Repository.Content.GetAllContentsByRef(
                            owner, repoName, filePath, run.HeadCommitSha);
                        
                        if (contents.Count > 0)
                        {
                            return new { Path = filePath, Content = contents[0].Content };
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("[{RunId}] Failed to fetch content for {FilePath}: {Message}", runId, filePath, ex.Message);
                    }
                    return null;
                });

                var results = await Task.WhenAll(tasks);
                foreach (var res in results.Where(r => r != null))
                {
                    fileContents[res!.Path] = res.Content;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch file contents for run {RunId}", runId);
            }
        }

        var result = new AnalysisRunDetailDto
        {
            Id = run.Id,
            RepoName = run.Repository.FullName ?? run.Repository.Name,
            BranchName = run.BranchName,
            Status = run.Status,
            FilesAnalyzed = run.FilesAnalyzed,
            AverageScore = (double)run.AverageScore,
            TotalIssues = run.TotalIssues,
            Summary = run.Summary,
            CreatedAt = run.CreatedAt,
            CompletedAt = run.CompletedAt,
            Issues = run.Issues.Select(i => new IssueDto
            {
                Id = i.Id,
                FilePath = i.FilePath,
                File = i.FilePath, // Backward compatibility
                LineStart = i.LineStart,
                LineEnd = i.LineEnd,
                Line = i.LineStart ?? 0, // Backward compatibility
                Severity = i.Severity,
                Category = i.Category,
                Message = i.Message,
                RuleId = i.RuleId,
                Rule = i.RuleId ?? string.Empty // Backward compatibility
            }).ToList(),
            FileContents = fileContents,
            FileMetrics = fileMetrics
        };

        // Cache the completed/failed result for 15 minutes
        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(15));

        return result;
    }

    public async Task<List<IssueDto>> GetAnalysisRunIssuesAsync(int runId, int userId, string? severity = null, string? category = null)
    {
        // Clear change tracker for fresh data
        _context.ChangeTracker.Clear();
        
        var query = _context.AnalysisIssues
            .AsNoTracking() // ⚡ KEY FIX - Don't use cached entities
            .Include(i => i.Run)
                .ThenInclude(r => r.Repository)
            .Where(i => i.RunId == runId && i.Run.Repository.UserId == userId);

        if (!string.IsNullOrEmpty(severity))
        {
            query = query.Where(i => i.Severity == severity);
        }

        if (!string.IsNullOrEmpty(category))
        {
            query = query.Where(i => i.Category == category);
        }

        var issues = await query.ToListAsync();

        return issues.Select(i => new IssueDto
        {
            Id = i.Id,
            FilePath = i.FilePath,
            File = i.FilePath, // Backward compatibility
            LineStart = i.LineStart,
            LineEnd = i.LineEnd,
            Line = i.LineStart ?? 0, // Backward compatibility
            Severity = i.Severity,
            Category = i.Category,
            Message = i.Message,
            RuleId = i.RuleId,
            Rule = i.RuleId ?? string.Empty // Backward compatibility
        }).ToList();
    }

    private async Task<AnalysisResultDto> BuildResultFromRunAsync(AnalysisRun run)
    {
        var (owner, repoName) = _githubClientService.ParseRepoUrl(run.Repository.CloneUrl);

        var issuesBySeverity = new Dictionary<string, int>();
        var fileMetrics = new Dictionary<string, FileMetricsDto>();

        // Get file metrics from AnalysisMetrics
        var metrics = await _context.AnalysisMetrics
            .Where(m => m.RunId == run.Id)
            .FirstOrDefaultAsync();

        if (metrics != null && !string.IsNullOrEmpty(metrics.MetricsJson))
        {
            try
            {
                fileMetrics = JsonSerializer.Deserialize<Dictionary<string, FileMetricsDto>>(metrics.MetricsJson) ?? new Dictionary<string, FileMetricsDto>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize file metrics for run {RunId}", run.Id);
            }
        }

        // Populate issues with same logic as GetAnalysisRunByIdAsync
        var issues = run.Issues.Select(i => new IssueDto
        {
            Id = i.Id,
            FilePath = i.FilePath,
            File = i.FilePath,
            LineStart = i.LineStart,
            LineEnd = i.LineEnd,
            Line = i.LineStart ?? 0,
            Severity = i.Severity,
            Category = i.Category,
            Message = i.Message,
            RuleId = i.RuleId,
            Rule = i.RuleId ?? string.Empty
        }).ToList();

        return new AnalysisResultDto
        {
            RunId = run.Id.ToString(),
            RepoName = $"{owner}/{repoName}",
            BranchName = run.BranchName,
            CommitSha = run.HeadCommitSha,
            FilesAnalyzed = run.FilesAnalyzed,
            AverageScore = (double)run.AverageScore,
            TotalIssues = run.TotalIssues,
            IssuesBySeverity = issuesBySeverity,
            FileMetrics = fileMetrics,
            Issues = issues,
            AnalyzedAt = run.CompletedAt ?? run.CreatedAt
        };
    }
}
