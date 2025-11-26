using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace dotnet_api.Models;

[Table("pull_requests")]
public class PullRequest
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("repository_id")]
    public int RepositoryId { get; set; }

    [Required]
    [Column("github_pr_id")]
    public long GithubPrId { get; set; }

    [Required]
    [Column("number")]
    public int Number { get; set; }

    [Required]
    [MaxLength(500)]
    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("state")]
    public string State { get; set; } = string.Empty;

    [Column("author_user_id")]
    public int? AuthorUserId { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    [ForeignKey("RepositoryId")]
    public virtual Repository Repository { get; set; } = null!;

    [ForeignKey("AuthorUserId")]
    public virtual User? AuthorUser { get; set; }

    public virtual ICollection<CodeReview> CodeReviews { get; set; } = new List<CodeReview>();
}

