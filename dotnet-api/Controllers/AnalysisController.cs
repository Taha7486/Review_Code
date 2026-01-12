using dotnet_api.Models.DTOs;
using dotnet_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace dotnet_api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AnalysisController : ControllerBase
{
    private readonly IAnalysisService _analysisService;
    private readonly BackgroundAnalysisProcessor _backgroundProcessor;
    private readonly ILogger<AnalysisController> _logger;

    public AnalysisController(
        IAnalysisService analysisService, 
        BackgroundAnalysisProcessor backgroundProcessor,
        ILogger<AnalysisController> logger)
    {
        _analysisService = analysisService;
        _backgroundProcessor = backgroundProcessor;
        _logger = logger;
    }

    private int GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(userIdClaim, out var userId))
            return userId;
        throw new UnauthorizedAccessException("User ID not found in token");
    }

    private ErrorResponseDto CreateErrorResponse(string code, string message, object? details = null)
    {
        var correlationId = HttpContext.Items["CorrelationId"]?.ToString();
        return new ErrorResponseDto
        {
            Code = code,
            Message = message,
            Details = details,
            CorrelationId = correlationId
        };
    }

    // POST: api/analysis/analyze
    [HttpPost("analyze")]
    public async Task<IActionResult> AnalyzeBranch([FromBody] AnalyzeBranchDto request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>()
                );
            return BadRequest(CreateErrorResponse(
                ErrorCodes.ValidationError,
                "Request validation failed",
                errors
            ));
        }

        try
        {
            var userId = GetUserId();
            var correlationId = HttpContext.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString("N")[..8];
            
            // Start analysis asynchronously - returns runId immediately
            var runId = await _analysisService.StartAnalysisAsync(request, userId, correlationId);
            
            // Check if this is an existing completed run - if so, don't queue a new job
            var existingRun = await _analysisService.GetAnalysisRunByIdAsync(runId, userId);
            if (existingRun != null && existingRun.Status == "completed")
            {
                _logger.LogDebug("[{CorrelationId}] Returning existing completed run {RunId}. FilesAnalyzed: {FileCount}, TotalIssues: {IssueCount}, Score: {Score}", 
                    correlationId, runId, existingRun.FilesAnalyzed, existingRun.TotalIssues, existingRun.AverageScore);
                return Accepted(new { 
                    runId, 
                    status = "completed", 
                    filesAnalyzed = existingRun.FilesAnalyzed,
                    totalIssues = existingRun.TotalIssues,
                    averageScore = existingRun.AverageScore,
                    message = "Analysis already completed. Results available." 
                });
            }
            
            // Queue the job for background processing (only for new/running runs)
            var job = new AnalysisJob
            {
                RunId = runId,
                Request = request,
                UserId = userId,
                CorrelationId = correlationId
            };
            
            var queued = await _backgroundProcessor.QueueAnalysisAsync(job);
            if (!queued)
            {
                _logger.LogWarning("[{CorrelationId}] Failed to queue analysis job for run {RunId}", correlationId, runId);
                return StatusCode(503, CreateErrorResponse(
                    ErrorCodes.InternalServerError,
                    "Failed to queue analysis job. Please try again."
                ));
            }
            
            // Return runId immediately - client should poll /runs/{id} for status
            return Accepted(new { runId, status = "running", message = "Analysis started. Poll /runs/{runId} for status." });
        }
        catch (Octokit.NotFoundException ex)
        {
            _logger.LogWarning(ex, "Repository or branch not found for user {UserId}", GetUserId());
            return NotFound(CreateErrorResponse(
                ErrorCodes.GitHubNotFound,
                "Repository or branch not found. Please verify the URL and branch name."
            ));
        }
        catch (Octokit.RateLimitExceededException ex)
        {
            _logger.LogWarning(ex, "GitHub rate limit exceeded");
            return StatusCode(429, CreateErrorResponse(
                ErrorCodes.GitHubRateLimitExceeded,
                "GitHub API rate limit exceeded. Please try again later."
            ));
        }
        catch (Octokit.AuthorizationException ex)
        {
            _logger.LogWarning(ex, "GitHub authorization failed");
            return StatusCode(401, CreateErrorResponse(
                ErrorCodes.GitHubUnauthorized,
                "Invalid GitHub Token. Please check your credentials."
            ));
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("PHP") || ex.Message.Contains("php-service"))
        {
            _logger.LogError(ex, "PHP service error during analysis");
            var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
            return StatusCode(503, CreateErrorResponse(
                ErrorCodes.PhpServiceError,
                "Analysis service is temporarily unavailable. Please try again later.",
                isDevelopment ? ex.Message : null
            ));
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "Analysis timeout");
            return StatusCode(504, CreateErrorResponse(
                ErrorCodes.AnalysisTimeout,
                "Analysis timed out. The repository may be too large. Please try again or contact support."
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during analysis");
            var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
            return StatusCode(500, CreateErrorResponse(
                ErrorCodes.AnalysisFailed,
                "An error occurred while analyzing the branch. Please try again later.",
                isDevelopment ? ex.Message : null
            ));
        }
    }

    // GET: api/analysis/branches
    [HttpGet("branches")]
    public async Task<IActionResult> GetBranches([FromQuery] string repoUrl, [FromQuery] string? githubToken = null)
    {
        if (string.IsNullOrEmpty(repoUrl))
            return BadRequest(CreateErrorResponse(
                ErrorCodes.MissingRequiredField,
                "RepoUrl query parameter is required"
            ));

        try
        {
            var branches = await _analysisService.GetRemoteBranchesAsync(repoUrl, githubToken);
            return Ok(branches);
        }
        catch (Octokit.RateLimitExceededException ex)
        {
            _logger.LogWarning(ex, "GitHub rate limit exceeded");
            return StatusCode(429, CreateErrorResponse(
                ErrorCodes.GitHubRateLimitExceeded,
                "GitHub API rate limit exceeded. Please try again later."
            ));
        }
        catch (Octokit.NotFoundException ex)
        {
            _logger.LogWarning(ex, "Repository not found: {RepoUrl}", repoUrl);
            return NotFound(CreateErrorResponse(
                ErrorCodes.GitHubNotFound,
                "Repository not found. Check the URL."
            ));
        }
        catch (Octokit.AuthorizationException ex)
        {
            _logger.LogWarning(ex, "GitHub authorization failed");
            return StatusCode(401, CreateErrorResponse(
                ErrorCodes.GitHubUnauthorized,
                "Invalid GitHub Token. Please check your credentials."
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching branches");
            var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
            return StatusCode(500, CreateErrorResponse(
                ErrorCodes.InternalServerError,
                "An error occurred while fetching branches. Please try again later.",
                isDevelopment ? ex.Message : null
            ));
        }
    }

    // GET: api/analysis/runs
    [HttpGet("runs")]
    public async Task<IActionResult> GetAnalysisRuns(
        [FromQuery] string? repoUrl = null,
        [FromQuery] string? branchName = null,
        [FromQuery] int limit = 20)
    {
        // Disable caching for history list to avoid stale data/score:0 issues
        Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["Expires"] = "0";
        
        try
        {
            var userId = GetUserId();
            var runs = await _analysisService.GetAnalysisRunsAsync(userId, repoUrl, branchName, limit);
            return Ok(runs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching analysis runs");
            var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
            return StatusCode(500, CreateErrorResponse(
                ErrorCodes.InternalServerError,
                "An error occurred while fetching analysis runs. Please try again later.",
                isDevelopment ? ex.Message : null
            ));
        }
    }

    [HttpGet("runs/{id}")]
    public async Task<IActionResult> GetAnalysisRunById(int id)
    {
        try
        {
            var userId = GetUserId();
            var run = await _analysisService.GetAnalysisRunByIdAsync(id, userId);

            if (run == null)
                return NotFound(CreateErrorResponse(
                    ErrorCodes.AnalysisRunNotFound,
                    $"Analysis run with ID {id} not found or you don't have access to it"
                ));

            // SMART CACHING: Only allow browser to cache if the analysis is fully done
            if (run.Status == "completed" || run.Status == "failed")
            {
                Response.Headers["Cache-Control"] = "public, max-age=300"; // Cache for 5 mins
                Response.Headers.Remove("Pragma");
                Response.Headers.Remove("Expires");
            }
            else
            {
                // Ensure browser NEVER caches a 'running' status
                Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
                Response.Headers["Pragma"] = "no-cache";
                Response.Headers["Expires"] = "0";
            }

            return Ok(run);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching analysis run {RunId}", id);
            var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
            return StatusCode(500, CreateErrorResponse(
                ErrorCodes.InternalServerError,
                "An error occurred while fetching the analysis run. Please try again later.",
                isDevelopment ? ex.Message : null
            ));
        }
    }

    // GET: api/analysis/runs/{id}/issues
    [HttpGet("runs/{id}/issues")]
    public async Task<IActionResult> GetAnalysisRunIssues(
        int id,
        [FromQuery] string? severity = null,
        [FromQuery] string? category = null)
    {
        try
        {
            var userId = GetUserId();
            var issues = await _analysisService.GetAnalysisRunIssuesAsync(id, userId, severity, category);
            return Ok(issues);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching issues for run {RunId}", id);
            var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
            return StatusCode(500, CreateErrorResponse(
                ErrorCodes.InternalServerError,
                "An error occurred while fetching issues. Please try again later.",
                isDevelopment ? ex.Message : null
            ));
        }
    }
}
