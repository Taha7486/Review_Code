using dotnet_api.Models;
using dotnet_api.Data;
using Microsoft.EntityFrameworkCore;

namespace dotnet_api.Services.Helpers;

/// <summary>
/// Handles repository database operations
/// </summary>
public interface IRepositoryService
{
    Task<Repository> GetOrCreateRepositoryAsync(int userId, string repoUrl, string? githubToken, string correlationId);
}

public class RepositoryService : IRepositoryService
{
    private readonly ApplicationDbContext _context;
    private readonly IGitHubClientService _gitHubClient;
    private readonly ILogger<RepositoryService> _logger;

    public RepositoryService(
        ApplicationDbContext context,
        IGitHubClientService gitHubClient,
        ILogger<RepositoryService> logger)
    {
        _context = context;
        _gitHubClient = gitHubClient;
        _logger = logger;
    }

    public async Task<Repository> GetOrCreateRepositoryAsync(int userId, string repoUrl, string? githubToken, string correlationId)
    {
        var sanitizedUrl = _gitHubClient.SanitizeRepoUrl(repoUrl);
        var (owner, repoName) = _gitHubClient.ParseRepoUrl(repoUrl);
        var fullUrl = $"https://github.com/{owner}/{repoName}";

        var repo = await _context.Repositories
            .FirstOrDefaultAsync(r => r.CloneUrl == fullUrl && r.UserId == userId);

        if (repo == null)
        {
            _logger.LogDebug("[{CorrelationId}] Creating new repository record for {RepoUrl}", correlationId, fullUrl);

            // Fetch actual GitHub repo details to get the real ID and fullName
            var client = _gitHubClient.GetAuthenticatedClient(githubToken);
            var githubRepo = await client.Repository.Get(owner, repoName);

            repo = new Repository
            {
                UserId = userId,
                Name = repoName,
                FullName = githubRepo.FullName,
                GithubRepoId = githubRepo.Id,
                CloneUrl = fullUrl,
                Provider = "github",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Repositories.Add(repo);
            await _context.SaveChangesAsync();

            _logger.LogDebug("[{CorrelationId}] Repository created with ID: {RepositoryId} (GitHub ID: {GithubRepoId})", 
                correlationId, repo.Id, repo.GithubRepoId);
        }
        else
        {
            _logger.LogDebug("[{CorrelationId}] Using existing repository ID: {RepositoryId}", correlationId, repo.Id);
        }

        return repo;
    }
}
