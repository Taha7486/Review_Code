namespace dotnet_api.Models.DTOs;

public class AnalysisResultDto
{
    public string Summary { get; set; } = string.Empty;
    public List<IssueDto> Issues { get; set; } = new List<IssueDto>();
    public Dictionary<string, string> FileContents { get; set; } = new Dictionary<string, string>();
    public int FilesAnalyzed { get; set; }
    public double AverageScore { get; set; }
    public int TotalIssues { get; set; }
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
    public string RunId { get; set; } = string.Empty;
    public string RepoName { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string CommitSha { get; set; } = string.Empty;
    public Dictionary<string, int> IssuesBySeverity { get; set; } = new Dictionary<string, int>();
    public Dictionary<string, FileMetricsDto> FileMetrics { get; set; } = new Dictionary<string, FileMetricsDto>();
}

public class IssueDto
{
    public int Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public int? LineStart { get; set; }
    public int? LineEnd { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty; // e.g., "critical", "major", "minor", "info"
    public string? RuleId { get; set; }
    public string Category { get; set; } = string.Empty; // e.g., "complexity", "security", "style"
    
    // Keep old properties for backward compatibility with PHP service response
    public string File { get; set; } = string.Empty;
    public int Line { get; set; }
    public string Rule { get; set; } = string.Empty;
}

public class FileMetricsDto
{
    public string ComplexityLevel { get; set; } = "low"; // low, medium, high, very_high
    public int FunctionCount { get; set; }
    public int LinesOfCode { get; set; }
}
