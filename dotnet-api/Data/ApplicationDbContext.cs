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
    public DbSet<PullRequest> PullRequests { get; set; }
    public DbSet<CodeReview> CodeReviews { get; set; }
    public DbSet<ReviewIssue> ReviewIssues { get; set; }
    public DbSet<Metric> Metrics { get; set; }
    public DbSet<ReviewConfiguration> ReviewConfigurations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure User entity
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.GithubUserId).HasDatabaseName("idx_users_github_user_id");
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

        // Configure PullRequest entity
        modelBuilder.Entity<PullRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.RepositoryId, e.Number })
                .IsUnique()
                .HasDatabaseName("idx_pr_repo_number");
            entity.HasOne(e => e.Repository)
                .WithMany(r => r.PullRequests)
                .HasForeignKey(e => e.RepositoryId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.AuthorUser)
                .WithMany(u => u.PullRequests)
                .HasForeignKey(e => e.AuthorUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configure CodeReview entity
        modelBuilder.Entity<CodeReview>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.PullRequestId).HasDatabaseName("idx_reviews_pr_id");
            entity.HasOne(e => e.PullRequest)
                .WithMany(pr => pr.CodeReviews)
                .HasForeignKey(e => e.PullRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure ReviewIssue entity
        modelBuilder.Entity<ReviewIssue>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ReviewId).HasDatabaseName("idx_issues_review_id");
            entity.HasOne(e => e.Review)
                .WithMany(cr => cr.ReviewIssues)
                .HasForeignKey(e => e.ReviewId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Metric entity
        modelBuilder.Entity<Metric>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ReviewId).HasDatabaseName("idx_metrics_review_id");
            entity.HasOne(e => e.Review)
                .WithMany(cr => cr.Metrics)
                .HasForeignKey(e => e.ReviewId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure ReviewConfiguration entity
        modelBuilder.Entity<ReviewConfiguration>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.User)
                .WithMany(u => u.ReviewConfigurations)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

