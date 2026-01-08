using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace dotnet_api.Models;

[Table("users")]
public class User
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("github_user_id")]
    public long? GithubUserId { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("username")]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("password_hash")]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(500)]
    [Column("avatar_url")]
    public string? AvatarUrl { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public virtual ICollection<Repository> Repositories { get; set; } = new List<Repository>();
    public virtual ICollection<PullRequest> PullRequests { get; set; } = new List<PullRequest>();
    public virtual ICollection<ReviewConfiguration> ReviewConfigurations { get; set; } = new List<ReviewConfiguration>();
}

