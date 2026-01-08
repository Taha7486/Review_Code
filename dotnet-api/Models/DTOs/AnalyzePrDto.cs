namespace dotnet_api.Models.DTOs;

public class AnalyzePrDto
{
    public string RepoUrl { get; set; } = string.Empty;
    public int PrNumber { get; set; }
    public string GithubToken { get; set; } = string.Empty; // Optional: if we need their personal token
}
