using System.Collections.Concurrent;
using dotnet_api.Data;
using Microsoft.EntityFrameworkCore;

namespace dotnet_api.Services;

public interface IMetricsService
{
    void RecordAnalysisDuration(double seconds, int fileCount, int issueCount);
    void RecordIssueDistribution(string severity, string category);
    Task<AnalysisMetricsDto> GetMetricsAsync(DateTime? since = null);
}

public class MetricsService : IMetricsService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MetricsService> _logger;
    private readonly ConcurrentDictionary<string, long> _issueDistribution = new();
    private readonly ConcurrentBag<AnalysisDurationMetric> _durationMetrics = new();

    public MetricsService(IServiceScopeFactory scopeFactory, ILogger<MetricsService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void RecordAnalysisDuration(double seconds, int fileCount, int issueCount)
    {
        _durationMetrics.Add(new AnalysisDurationMetric
        {
            Duration = seconds,
            FileCount = fileCount,
            IssueCount = issueCount,
            Timestamp = DateTime.UtcNow
        });

        // Keep only last 1000 metrics in memory
        if (_durationMetrics.Count > 1000)
        {
            var oldest = _durationMetrics.OrderBy(m => m.Timestamp).First();
            _durationMetrics.TryTake(out _);
        }
    }

    public void RecordIssueDistribution(string severity, string category)
    {
        var key = $"{severity}:{category}";
        _issueDistribution.AddOrUpdate(key, 1, (k, v) => v + 1);
    }

    public async Task<AnalysisMetricsDto> GetMetricsAsync(DateTime? since = null)
    {
        var cutoff = since ?? DateTime.UtcNow.AddHours(-24);
        
        // Create a scope to resolve the DbContext
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Get metrics from database
        var runs = await context.AnalysisRuns
            .Where(r => r.CreatedAt >= cutoff && r.Status == "completed")
            .ToListAsync();

        var totalRuns = runs.Count;
        var avgDuration = runs.Any() 
            ? runs.Average(r => r.CompletedAt.HasValue 
                ? (r.CompletedAt.Value - r.CreatedAt).TotalSeconds 
                : 0)
            : 0;
        
        var totalFiles = runs.Sum(r => r.FilesAnalyzed);
        var totalIssues = runs.Sum(r => r.TotalIssues);
        var avgScore = runs.Any() ? (double)runs.Average(r => r.AverageScore) : 0;

        // Get issue distribution from database
        var issues = await context.AnalysisIssues
            .Where(i => i.CreatedAt >= cutoff)
            .GroupBy(i => new { i.Severity, i.Category })
            .Select(g => new { g.Key.Severity, g.Key.Category, Count = g.Count() })
            .ToListAsync();

        var issueDistribution = issues.ToDictionary(
            i => $"{i.Severity}:{i.Category}",
            i => (long)i.Count
        );

        // Merge with in-memory metrics
        foreach (var kvp in _issueDistribution)
        {
            issueDistribution.TryGetValue(kvp.Key, out var existing);
            issueDistribution[kvp.Key] = existing + kvp.Value;
        }

        return new AnalysisMetricsDto
        {
            TotalRuns = totalRuns,
            AverageDuration = avgDuration,
            TotalFilesAnalyzed = totalFiles,
            TotalIssues = totalIssues,
            AverageScore = avgScore,
            IssueDistribution = issueDistribution,
            PeriodStart = cutoff,
            PeriodEnd = DateTime.UtcNow
        };
    }

    private class AnalysisDurationMetric
    {
        public double Duration { get; set; }
        public int FileCount { get; set; }
        public int IssueCount { get; set; }
        public DateTime Timestamp { get; set; }
    }
}

public class AnalysisMetricsDto
{
    public int TotalRuns { get; set; }
    public double AverageDuration { get; set; }
    public int TotalFilesAnalyzed { get; set; }
    public int TotalIssues { get; set; }
    public double AverageScore { get; set; }
    public Dictionary<string, long> IssueDistribution { get; set; } = new();
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
}
