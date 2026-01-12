namespace dotnet_api.Models.DTOs;

public class AnalysisRunListItemDto
{
    public int Id { get; set; }
    public int RepositoryId { get; set; }
    public string RepoName { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string DefaultBranch { get; set; } = string.Empty;
    public string BaseCommitSha { get; set; } = string.Empty;
    public string HeadCommitSha { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int FilesAnalyzed { get; set; }
    public double AverageScore { get; set; }
    public int TotalIssues { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class AnalysisRunDetailDto
{
    public int Id { get; set; }
    public int RepositoryId { get; set; }
    public string RepoName { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string DefaultBranch { get; set; } = string.Empty;
    public string BaseCommitSha { get; set; } = string.Empty;
    public string HeadCommitSha { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int FilesAnalyzed { get; set; }
    public double AverageScore { get; set; }
    public int TotalIssues { get; set; }
    public string? Summary { get; set; }
    public Dictionary<string, int> IssuesBySeverity { get; set; } = new Dictionary<string, int>();
    public Dictionary<string, FileMetricsDto> FileMetrics { get; set; } = new Dictionary<string, FileMetricsDto>();
    public List<IssueDto> Issues { get; set; } = new List<IssueDto>();
    public Dictionary<string, string> FileContents { get; set; } = new Dictionary<string, string>();
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
