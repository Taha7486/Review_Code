using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace dotnet_api.Models;

[Table("analysis_runs")]
public class AnalysisRun
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("repository_id")]
    public int RepositoryId { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("branch_name")]
    public string BranchName { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    [Column("default_branch")]
    public string DefaultBranch { get; set; } = string.Empty;

    [Required]
    [MaxLength(40)]
    [Column("base_commit_sha")]
    public string BaseCommitSha { get; set; } = string.Empty;

    [Required]
    [MaxLength(40)]
    [Column("head_commit_sha")]
    public string HeadCommitSha { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("status")]
    public string Status { get; set; } = "queued"; // queued, running, completed, failed

    [Required]
    [Column("files_analyzed")]
    public int FilesAnalyzed { get; set; }

    [Required]
    [Column("average_score", TypeName = "DECIMAL(5,2)")]
    public decimal AverageScore { get; set; }

    [Required]
    [Column("total_issues")]
    public int TotalIssues { get; set; }

    [Column("summary", TypeName = "TEXT")]
    public string? Summary { get; set; }

    [Column("raw_output", TypeName = "LONGTEXT")]
    public string? RawOutput { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    // Navigation properties
    [ForeignKey("RepositoryId")]
    public virtual Repository Repository { get; set; } = null!;

    public virtual ICollection<AnalysisIssue> Issues { get; set; } = new List<AnalysisIssue>();
    public virtual ICollection<AnalysisMetric> Metrics { get; set; } = new List<AnalysisMetric>();
}

[Table("analysis_issues")]
public class AnalysisIssue
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("run_id")]
    public int RunId { get; set; }

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
    [Column("category")]
    public string Category { get; set; } = string.Empty; // security, complexity, style

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
    [ForeignKey("RunId")]
    public virtual AnalysisRun Run { get; set; } = null!;
}

[Table("analysis_metrics")]
public class AnalysisMetric
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("run_id")]
    public int RunId { get; set; }

    [Required]
    [Column("metrics_json", TypeName = "JSON")]
    public string MetricsJson { get; set; } = string.Empty;

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    [ForeignKey("RunId")]
    public virtual AnalysisRun Run { get; set; } = null!;
}
