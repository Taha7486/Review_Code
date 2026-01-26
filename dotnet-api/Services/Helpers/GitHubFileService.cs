using Octokit;
using dotnet_api.Services.Helpers;
using System.IO.Compression;
using System.Text;

namespace dotnet_api.Services.Helpers;

/// <summary>
/// Handles fetching code files from GitHub repositories using high-performance Zipball retrieval
/// </summary>
public interface IGitHubFileService
{
    Task<List<PullRequestFileDetail>> GetCodeFromBranchAsync(
        string owner,
        string repoName,
        string branchName,
        string? defaultBranch,
        string correlationId,
        GitHubClient client);

    Task<List<PullRequestFileDetail>> GetAllFilesFromBranchAsync(
        string owner,
        string repoName,
        string branchRef,
        string correlationId,
        GitHubClient client);
}

public class GitHubFileService : IGitHubFileService
{
    private readonly IFileFilter _fileFilter;
    private readonly ILogger<GitHubFileService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IPrometheusMetricsService _prometheusMetrics;
    private const long MaxArchiveSize = 25 * 1024 * 1024; // 25MB safety limit

    public GitHubFileService(
        IFileFilter fileFilter, 
        ILogger<GitHubFileService> logger, 
        IHttpClientFactory httpClientFactory,
        IPrometheusMetricsService prometheusMetrics)
    {
        _fileFilter = fileFilter;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _prometheusMetrics = prometheusMetrics;
    }

    public async Task<List<PullRequestFileDetail>> GetCodeFromBranchAsync(
        string owner,
        string repoName,
        string branchName,
        string? defaultBranch,
        string correlationId,
        GitHubClient client)
    {
        _logger.LogInformation("[{CorrelationId}] Starting Zipball retrieval for {Owner}/{Repo} branch {Branch}",
            correlationId, owner, repoName, branchName);

        // Track GitHub rate limit before API call
        try
        {
            var rateLimit = await client.RateLimit.GetRateLimits();
            _prometheusMetrics.RecordGitHubRateLimit(
                remaining: rateLimit.Resources.Core.Remaining,
                limit: rateLimit.Resources.Core.Limit
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[{CorrelationId}] Failed to fetch GitHub rate limit", correlationId);
        }

        var allFiles = await GetAllFilesFromBranchAsync(owner, repoName, branchName, correlationId, client);

        _logger.LogInformation("[{CorrelationId}] Successfully retrieved and filtered {FileCount} files from archive",
            correlationId, allFiles.Count);

        return allFiles;
    }

    public async Task<List<PullRequestFileDetail>> GetAllFilesFromBranchAsync(
        string owner,
        string repoName,
        string branchRef,
        string correlationId,
        GitHubClient client)
    {
        var files = new List<PullRequestFileDetail>();

        try
        {
            // 🚀 Streaming: Get Link -> Stream
            // Construct URL manually to enable streaming (Octokit only supports byte[] download)
            var baseUrl = client.Connection.BaseAddress.ToString().TrimEnd('/');
            var archiveUrl = $"{baseUrl}/repos/{owner}/{repoName}/zipball/{branchRef}";
            
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CodeReviewTool-Analysis/1.0");

            if (client.Credentials != null && client.Credentials.AuthenticationType == AuthenticationType.Oauth && !string.IsNullOrEmpty(client.Credentials.Password))
            {
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", client.Credentials.Password);
            }

            HttpResponseMessage? response = null;
            int maxRetries = 3;

            for (int i = 0; i <= maxRetries; i++)
            {
                try
                {
                    _logger.LogDebug("[{CorrelationId}] Downloading Zipball stream (Attempt {Attempt}/{Max})...", correlationId, i + 1, maxRetries + 1);
                    
                    // Use ResponseHeadersRead for streaming
                    var request = new HttpRequestMessage(HttpMethod.Get, archiveUrl);
                    var currentResponse = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                    if (currentResponse.IsSuccessStatusCode)
                    {
                        response = currentResponse;
                        break;
                    }
                    
                    // Handle Rate Limits (403 or 429)
                    if (currentResponse.StatusCode == System.Net.HttpStatusCode.Forbidden || (int)currentResponse.StatusCode == 429)
                    {
                        var retryAfter = currentResponse.Headers.RetryAfter?.Delta;
                        
                        if (i == maxRetries)
                        {
                            _logger.LogError("[{CorrelationId}] GitHub rate limit exceeded after {Max} retries. Status: {Status}", correlationId, maxRetries, currentResponse.StatusCode);
                            using (currentResponse) // Ensure disposal before throwing
                            {
                                currentResponse.EnsureSuccessStatusCode(); // Will throw
                            }
                        }

                        var delay = retryAfter ?? TimeSpan.FromSeconds(Math.Pow(2, i + 1));
                        _logger.LogWarning("[{CorrelationId}] GitHub rate limit hit. Retrying in {Delay}s...", correlationId, delay.TotalSeconds);
                        await Task.Delay(delay);
                        currentResponse.Dispose(); // Proper cleanup of failed response
                        continue;
                    }

                    // Dispose failed response and throw for other errors
                    using (currentResponse)
                    {
                        currentResponse.EnsureSuccessStatusCode(); // Will throw
                    }
                }
                catch (HttpRequestException ex)
                {
                    response?.Dispose(); // Clean up any partial response
                    response = null;
                    if (i == maxRetries) throw;
                    _logger.LogWarning(ex, "[{CorrelationId}] Network error downloading zipball. Retrying...", correlationId);
                    await Task.Delay(TimeSpan.FromSeconds(2));
                }
            }

            if (response == null || !response.IsSuccessStatusCode)
            {
                _logger.LogError("[{CorrelationId}] Failed to download archive", correlationId);
                 if (response != null) throw new HttpRequestException($"Failed to download archive: {response.StatusCode}");
                 throw new HttpRequestException("Failed to download archive");
            }

            // Check Content-Length if available
            if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value > MaxArchiveSize)
            {
                _logger.LogWarning("[{CorrelationId}] Archive size ({Size}MB) exceeds safety limit", correlationId, response.Content.Headers.ContentLength.Value / (1024 * 1024));
                response.Dispose();
                throw new InvalidOperationException("Repository is too large to analyze via Zipball.");
            }

            // Process the archive with proper disposal
            using (response)
            using (var zipStream = await response.Content.ReadAsStreamAsync())
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
            {

            // GitHub Zipballs always have a root folder like "owner-repo-sha/"
            // We need to identify it so we can strip it from the file paths
            string? rootFolderPrefix = archive.Entries.FirstOrDefault()?.FullName.Split('/')[0];
            
            _logger.LogDebug("[{CorrelationId}] Processing archive with {EntryCount} entries. Root prefix: {Prefix}", 
                correlationId, archive.Entries.Count, rootFolderPrefix);

            foreach (var entry in archive.Entries)
            {
                // Skip directories
                if (entry.FullName.EndsWith("/")) continue;

                // Normalize path (strip the "owner-repo-sha/" prefix)
                string normalizedPath = entry.FullName;
                if (!string.IsNullOrEmpty(rootFolderPrefix) && normalizedPath.StartsWith(rootFolderPrefix + "/"))
                {
                    normalizedPath = normalizedPath.Substring(rootFolderPrefix.Length + 1);
                }

                // Apply our standard file filters
                if (!_fileFilter.ShouldAnalyzeFile(normalizedPath, entry.Length))
                    continue;

                try
                {
                    using var stream = entry.Open();
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    var content = await reader.ReadToEndAsync();

                    if (_fileFilter.IsBinaryContent(content))
                    {
                        continue;
                    }

                    files.Add(new PullRequestFileDetail
                    {
                        FileName = normalizedPath,
                        Content = content
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[{CorrelationId}] Failed to process file from archive: {File}", correlationId, entry.FullName);
                }
            }
            } // End using blocks for archive processing
        }
        catch (NotFoundException)
        {
            _logger.LogWarning("[{CorrelationId}] Branch or repository not found: {Branch}", correlationId, branchRef);
            throw;
        }
        catch (Exception ex) when (ex is not InvalidOperationException && ex is not NotFoundException)
        {
            _logger.LogError(ex, "[{CorrelationId}] Unexpected error during Zipball extraction", correlationId);
            throw;
        }

        return files;
    }
}
