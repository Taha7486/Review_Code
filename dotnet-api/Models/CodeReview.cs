using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace dotnet_api.Models;

[Table("code_reviews")]
public class CodeReview
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("pull_request_id")]
    public int PullRequestId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("status")]
    public string Status { get; set; } = string.Empty;

    [Column("summary", TypeName = "TEXT")]
    public string? Summary { get; set; }

    [Column("raw_output", TypeName = "JSON")]
    public string? RawOutput { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    // Navigation properties
    [ForeignKey("PullRequestId")]
    public virtual PullRequest PullRequest { get; set; } = null!;

    public virtual ICollection<ReviewIssue> ReviewIssues { get; set; } = new List<ReviewIssue>();
    public virtual ICollection<Metric> Metrics { get; set; } = new List<Metric>();
}

