using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace dotnet_api.Models;

[Table("metrics")]
public class Metric
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("review_id")]
    public int ReviewId { get; set; }

    [Required]
    [Column("metrics_json", TypeName = "JSON")]
    public string MetricsJson { get; set; } = string.Empty;

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    [ForeignKey("ReviewId")]
    public virtual CodeReview Review { get; set; } = null!;
}

