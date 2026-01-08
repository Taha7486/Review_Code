namespace dotnet_api.Models.DTOs;

public class AnalysisResultDto
{
    public string Summary { get; set; } = string.Empty;
    public List<IssueDto> Issues { get; set; } = new List<IssueDto>();
}

public class IssueDto
{
    public string File { get; set; } = string.Empty;
    public int Line { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty; // e.g., "error", "warning"
    public string Rule { get; set; } = string.Empty;
}
