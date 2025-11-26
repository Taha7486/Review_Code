using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace dotnet_api.Models;

[Table("review_issues")]
public class ReviewIssue
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("review_id")]
    public int ReviewId { get; set; }

    [Required]
    [MaxLength(500)]
    [Column("file_path")]
    public string FilePath { get; set; } = string.Empty;

    [Column("line_start")]
    public int? LineStart { get; set; }

    [Column("line_end")]
    public int? LineEnd { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("severity")]
    public string Severity { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("type")]
    public string Type { get; set; } = string.Empty;

    [Required]
    [Column("message", TypeName = "TEXT")]
    public string Message { get; set; } = string.Empty;

    [MaxLength(100)]
    [Column("rule_id")]
    public string? RuleId { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    [ForeignKey("ReviewId")]
    public virtual CodeReview Review { get; set; } = null!;
}

