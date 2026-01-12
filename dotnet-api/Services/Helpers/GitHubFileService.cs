using Octokit;
using dotnet_api.Services.Helpers;

namespace dotnet_api.Services.Helpers;

/// <summary>
/// Handles fetching code files from GitHub repositories
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

    public GitHubFileService(IFileFilter fileFilter, ILogger<GitHubFileService> logger)
    {
        _fileFilter = fileFilter;
        _logger = logger;
    }

    public async Task<List<PullRequestFileDetail>> GetCodeFromBranchAsync(
        string owner,
        string repoName,
        string branchName,
        string? defaultBranch,
        string correlationId,
        GitHubClient client)
    {
        _logger.LogDebug("[{CorrelationId}] Fetching files from {Owner}/{Repo} branch {Branch}",
            correlationId, owner, repoName, branchName);

        var allFiles = await GetAllFilesFromBranchAsync(owner, repoName, branchName, correlationId, client);

        _logger.LogDebug("[{CorrelationId}] Found {FileCount} files in branch {Branch}",
            correlationId, allFiles.Count, branchName);

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
            var branch = await client.Repository.Branch.Get(owner, repoName, branchRef);
            var tree = await client.Git.Tree.GetRecursive(owner, repoName, branch.Commit.Sha);

            foreach (var item in tree.Tree.Where(t => t.Type == TreeType.Blob))
            {
                if (!_fileFilter.ShouldAnalyzeFile(item.Path, item.Size))
                    continue;

                try
                {
                    var blob = await client.Git.Blob.Get(owner, repoName, item.Sha);
                    var content = blob.Encoding.Equals(EncodingType.Base64)
                        ? System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(blob.Content))
                        : blob.Content;

                    if (_fileFilter.IsBinaryContent(content))
                    {
                        _logger.LogDebug("[{CorrelationId}] Skipping binary file: {File}", correlationId, item.Path);
                        continue;
                    }

                    files.Add(new PullRequestFileDetail
                    {
                        FileName = item.Path,
                        Content = content
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[{CorrelationId}] Failed to fetch file {File}", correlationId, item.Path);
                }
            }
        }
        catch (NotFoundException)
        {
            _logger.LogWarning("[{CorrelationId}] Branch {Branch} not found", correlationId, branchRef);
            throw;
        }

        _logger.LogDebug("[{CorrelationId}] Retrieved {FileCount} analyzable files", correlationId, files.Count);
        return files;
    }
}
