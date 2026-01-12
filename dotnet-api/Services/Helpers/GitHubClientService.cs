using Octokit;

namespace dotnet_api.Services.Helpers;

/// <summary>
/// Handles GitHub API interactions and authentication
/// </summary>
public interface IGitHubClientService
{
    GitHubClient GetAuthenticatedClient(string? githubToken);
    Task<IReadOnlyList<string>> GetRemoteBranchesAsync(string repoUrl, string? githubToken);
    (string owner, string repoName) ParseRepoUrl(string repoUrl);
    string SanitizeRepoUrl(string repoUrl);
}

public class GitHubClientService : IGitHubClientService
{
    private readonly IConfiguration _config;
    private readonly ILogger<GitHubClientService> _logger;

    public GitHubClientService(IConfiguration config, ILogger<GitHubClientService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public GitHubClient GetAuthenticatedClient(string? githubToken)
    {
        var client = new GitHubClient(new ProductHeaderValue("CodeReviewTool"));

        // Priority 1: User-provided token from UI
        if (!string.IsNullOrWhiteSpace(githubToken))
        {
            _logger.LogDebug("Using provided GitHub token for authentication");
            client.Credentials = new Credentials(githubToken);
        }
        // Priority 2: System-wide GITHUB_PAT from .env
        else
        {
            var systemToken = Environment.GetEnvironmentVariable("GITHUB_PAT");
            if (!string.IsNullOrWhiteSpace(systemToken))
            {
                _logger.LogDebug("Using system GITHUB_PAT for authentication");
                client.Credentials = new Credentials(systemToken);
            }
            else
            {
                _logger.LogWarning("No GitHub token or GITHUB_PAT available. Using unauthenticated access (rate limits apply)");
            }
        }

        return client;
    }

    public async Task<IReadOnlyList<string>> GetRemoteBranchesAsync(string repoUrl, string? githubToken)
    {
        var client = GetAuthenticatedClient(githubToken);
        var (owner, repoName) = ParseRepoUrl(repoUrl);
        var branches = await client.Repository.Branch.GetAll(owner, repoName);
        return branches.Select(b => b.Name).ToList();
    }

    public (string owner, string repoName) ParseRepoUrl(string repoUrl)
    {
        var sanitized = SanitizeRepoUrl(repoUrl);
        var parts = sanitized.Split('/');
        if (parts.Length >= 2)
        {
            return (parts[^2], parts[^1]);
        }
        throw new ArgumentException($"Invalid GitHub URL format: {repoUrl}");
    }

    public string SanitizeRepoUrl(string repoUrl)
    {
        if (string.IsNullOrWhiteSpace(repoUrl))
        {
            throw new ArgumentNullException(nameof(repoUrl), "Repository URL cannot be null or empty");
        }

        repoUrl = repoUrl.Trim();

        if (repoUrl.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
        {
            repoUrl = repoUrl.Substring("https://github.com/".Length);
        }
        else if (repoUrl.StartsWith("http://github.com/", StringComparison.OrdinalIgnoreCase))
        {
            repoUrl = repoUrl.Substring("http://github.com/".Length);
        }

        repoUrl = repoUrl.TrimEnd('/');
        if (repoUrl.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            repoUrl = repoUrl.Substring(0, repoUrl.Length - 4);
        }

        return repoUrl;
    }
}
