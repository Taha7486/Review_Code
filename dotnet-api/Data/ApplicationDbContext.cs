using Microsoft.EntityFrameworkCore;
using dotnet_api.Models;

namespace dotnet_api.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // DbSets for all entities
    public DbSet<User> Users { get; set; }
    public DbSet<Repository> Repositories { get; set; }
    public DbSet<AnalysisRun> AnalysisRuns { get; set; }
    public DbSet<AnalysisIssue> AnalysisIssues { get; set; }
    public DbSet<AnalysisMetric> AnalysisMetrics { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure User entity
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.HasIndex(e => e.Email).IsUnique();
        });

        // Configure Repository entity
        modelBuilder.Entity<Repository>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.GithubRepoId).IsUnique().HasDatabaseName("idx_repos_github_repo_id");
            entity.HasOne(e => e.User)
                .WithMany(u => u.Repositories)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure AnalysisRun entity
        modelBuilder.Entity<AnalysisRun>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.RepositoryId, e.BranchName })
                .HasDatabaseName("idx_runs_repo_branch");
            entity.HasIndex(e => new { e.RepositoryId, e.BaseCommitSha, e.HeadCommitSha })
                .IsUnique()
                .HasDatabaseName("idx_runs_repo_commits");
            entity.HasIndex(e => e.Status)
                .HasDatabaseName("idx_runs_status");
            entity.HasOne(e => e.Repository)
                .WithMany(r => r.AnalysisRuns)
                .HasForeignKey(e => e.RepositoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure AnalysisIssue entity
        modelBuilder.Entity<AnalysisIssue>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.RunId)
                .HasDatabaseName("idx_ai_run");
            entity.HasIndex(e => new { e.RunId, e.Severity })
                .HasDatabaseName("idx_ai_run_sev");
            entity.HasOne(e => e.Run)
                .WithMany(r => r.Issues)
                .HasForeignKey(e => e.RunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure AnalysisMetric entity
        modelBuilder.Entity<AnalysisMetric>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.RunId)
                .HasDatabaseName("idx_am_run");
            entity.HasOne(e => e.Run)
                .WithMany(r => r.Metrics)
                .HasForeignKey(e => e.RunId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

