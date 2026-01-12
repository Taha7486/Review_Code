using System.ComponentModel.DataAnnotations;

namespace dotnet_api.Models.DTOs;

public class AnalyzeBranchDto
{
    [Required(ErrorMessage = "Repository URL is required")]
    [Url(ErrorMessage = "Invalid URL format")]
    public string RepoUrl { get; set; } = string.Empty;

    [Required(ErrorMessage = "Branch name is required")]
    public string BranchName { get; set; } = string.Empty;

    public string? GithubToken { get; set; } // Optional
}
