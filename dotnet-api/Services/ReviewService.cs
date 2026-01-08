using dotnet_api.Models.DTOs;
using Octokit;
using System.Text.Json;

namespace dotnet_api.Services;

public interface IReviewService
{
    Task<AnalysisResultDto> AnalyzePullRequestAsync(AnalyzePrDto request);
}

public class ReviewService : IReviewService
{
    private readonly GitHubClient _githubClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;

    public ReviewService(IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
        _githubClient = new GitHubClient(new ProductHeaderValue("CodeReviewTool"));
    }

    public async Task<AnalysisResultDto> AnalyzePullRequestAsync(AnalyzePrDto request)
    {
        // 1. Fetch Code from GitHub (Octokit)
        var prFiles = await GetCodeFromGitHub(request.RepoUrl, request.PrNumber);

        if (prFiles.Count == 0)
        {
            return new AnalysisResultDto 
            { 
                Summary = "No files found to analyze (or authentication required).", 
                Issues = new List<IssueDto>() 
            };
        }

        // 2. Send to PHP Service for Analysis
        var analysisResult = await AnalyzeWithPhp(prFiles);

        // 3. Return Results Immediately (Pass-through)
        return analysisResult;
    }

    private async Task<AnalysisResultDto> AnalyzeWithPhp(List<PullRequestFileDetail> files)
    {
        // Fetch dynamic URL from configuration
        var phpServiceUrl = _config["ServiceUrls:PhpAnalysisApi"] 
                            ?? throw new ArgumentNullException("ServiceUrls:PhpAnalysisApi is missing in appsettings.json");

        var payload = new 
        { 
            files = files.Select(f => new { path = f.FileName, content = f.Patch }).ToList()
        };
        var jsonContent = new StringContent( JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");

        try
        {
            var client = _httpClientFactory.CreateClient(); // ✅ Correct way to get a client
            var response = await client.PostAsync(phpServiceUrl, jsonContent);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            
            // Deserialize into the structure PHP actually returns
            var phpResult = JsonSerializer.Deserialize<PhpAnalysisResponse>(content, options);

            if (phpResult == null) return new AnalysisResultDto { Summary = "Received empty response from PHP service." };

            // Flatten results to our DTO
            var finalResult = new AnalysisResultDto
            {
                Summary = $"Analyzed {phpResult.files_analyzed} files. Score: {phpResult.average_score}/100. Issues: {phpResult.total_issues}",
                Issues = new List<IssueDto>()
            };

            foreach (var fileResult in phpResult.results)
            {
                foreach (var issue in fileResult.issues)
                {
                    finalResult.Issues.Add(new IssueDto
                    {
                        File = fileResult.file_path,
                        Line = issue.line,
                        Message = issue.message,
                        Severity = issue.severity,
                        Rule = issue.rule ?? "general_issue"
                    });
                }
            }

            return finalResult;
        }
        catch (Exception ex)
        {
            return new AnalysisResultDto 
            { 
                Summary = $"Analysis Failed: {ex.Message}", 
                Issues = new List<IssueDto>() 
            };
        }
    }

    private async Task<List<PullRequestFileDetail>> GetCodeFromGitHub(string repoUrl, int prNumber)
    {
        var (owner, name) = ParseRepoUrl(repoUrl);
        var files = await _githubClient.PullRequest.Files(owner, name, prNumber);
        
        return files.Select(f => new PullRequestFileDetail
        {
            FileName = f.FileName,
            Patch = f.Patch,
            Status = f.Status
        }).ToList();
    }

    private (string owner, string name) ParseRepoUrl(string url)
    {
        var uri = new Uri(url);
        var segments = uri.AbsolutePath.Trim('/').Split('/');
        if (segments.Length < 2) throw new ArgumentException("Invalid Repository URL");
        return (segments[0], segments[1]);
    }
}

// Helper class for internal use
public class PullRequestFileDetail
{
    public string FileName { get; set; } = string.Empty;
    public string Patch { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

// --- PHP Response Classes ---
public class PhpAnalysisResponse
{
    public int files_analyzed { get; set; }
    public double average_score { get; set; }
    public int total_issues { get; set; }
    public List<PhpFileAnalysis> results { get; set; } = new List<PhpFileAnalysis>();
}

public class PhpFileAnalysis
{
    public string file_path { get; set; } = string.Empty;
    public List<PhpIssue> issues { get; set; } = new List<PhpIssue>();
}

public class PhpIssue
{
    public int line { get; set; }
    public string message { get; set; } = string.Empty;
    public string severity { get; set; } = string.Empty;
    public string rule { get; set; } = string.Empty;
}
