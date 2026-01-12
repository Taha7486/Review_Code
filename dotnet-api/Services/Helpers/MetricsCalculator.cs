using dotnet_api.Models;
using dotnet_api.Models.DTOs;
using dotnet_api.Data;
using System.Text.Json;

namespace dotnet_api.Services.Helpers;

/// <summary>
/// Handles metrics calculation and database storage
/// </summary>
public interface IMetricsCalculator
{
    Dictionary<string, int> CalculateIssuesBySeverity(List<PhpFileAnalysis> results);
    Dictionary<string, FileMetricsDto> CalculateFileMetrics(List<PhpFileAnalysis> results);
    Task SaveIssuesAndMetricsAsync(
        int runId,
        PhpAnalysisResponse phpResult,
        List<PullRequestFileDetail> files,
        string? headCommitSha,
        string correlationId);
}

public class MetricsCalculator : IMetricsCalculator
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<MetricsCalculator> _logger;

    public MetricsCalculator(ApplicationDbContext context, ILogger<MetricsCalculator> logger)
    {
        _context = context;
        _logger = logger;
    }

    public Dictionary<string, int> CalculateIssuesBySeverity(List<PhpFileAnalysis> results)
    {
        var severityCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "error", 0 },
            { "warning", 0 },
            { "info", 0 }
        };

        foreach (var fileResult in results)
        {
            foreach (var issue in fileResult.Issues)
            {
                var severity = issue.Severity?.ToLower() ?? "info";
                if (severityCount.ContainsKey(severity))
                {
                    severityCount[severity]++;
                }
                else
                {
                    severityCount[severity] = 1;
                }
            }
        }

        return severityCount;
    }

    public Dictionary<string, FileMetricsDto> CalculateFileMetrics(List<PhpFileAnalysis> results)
    {
        var metrics = new Dictionary<string, FileMetricsDto>();

        foreach (var fileResult in results)
        {
            var errorCount = fileResult.Issues.Count(i => i.Severity.Equals("error", StringComparison.OrdinalIgnoreCase));
            var warningCount = fileResult.Issues.Count(i => i.Severity.Equals("warning", StringComparison.OrdinalIgnoreCase));
            
            // Infer complexity based on issue count and score
            string complexityLevel = "low";
            if (fileResult.Score < 50 || errorCount > 10)
                complexityLevel = "very_high";
            else if (fileResult.Score < 70 || errorCount > 5)
                complexityLevel = "high";
            else if (fileResult.Score < 85 || warningCount > 10)
                complexityLevel = "medium";

            var fileMetrics = new FileMetricsDto
            {
                ComplexityLevel = complexityLevel,
                FunctionCount = 0, // Could be populated if PHP service provides this
                LinesOfCode = 0    // Could be populated if PHP service provides this
            };

            var fileName = !string.IsNullOrEmpty(fileResult.FilePath) ? fileResult.FilePath : fileResult.File;
            metrics[fileName] = fileMetrics;
        }

        return metrics;
    }

    public async Task SaveIssuesAndMetricsAsync(
        int runId,
        PhpAnalysisResponse phpResult,
        List<PullRequestFileDetail> files,
        string? headCommitSha,
        string correlationId)
    {
        _logger.LogInformation("[{CorrelationId}] Saving {IssueCount} issues for run {RunId}",
            correlationId, phpResult.TotalIssues, runId);

        var issues = new List<AnalysisIssue>();

        foreach (var fileResult in phpResult.Results)
        {
            var fileName = !string.IsNullOrEmpty(fileResult.FilePath) ? fileResult.FilePath : fileResult.File;
            
            foreach (var issue in fileResult.Issues)
            {
                var line = issue.LineNumber > 0 ? issue.LineNumber : issue.Line;
                
                issues.Add(new AnalysisIssue
                {
                    RunId = runId,
                    FilePath = fileName,
                    LineStart = line,
                    LineEnd = line,
                    Severity = issue.Severity ?? "info",
                    Category = issue.Category ?? InferCategory(issue.Message),
                    Message = issue.Message,
                    RuleId = null,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        if (issues.Any())
        {
            await _context.AnalysisIssues.AddRangeAsync(issues);
        }

        // Save metrics
        var fileMetrics = CalculateFileMetrics(phpResult.Results);
        var metricsJson = JsonSerializer.Serialize(fileMetrics);

        var metrics = new AnalysisMetric
        {
            RunId = runId,
            MetricsJson = metricsJson,
            CreatedAt = DateTime.UtcNow
        };

        await _context.AnalysisMetrics.AddAsync(metrics);
        await _context.SaveChangesAsync();

        _logger.LogDebug("[{CorrelationId}] Saved {IssueCount} issues and metrics for run {RunId}",
            correlationId, issues.Count, runId);
    }

    private string InferCategory(string message)
    {
        var lowerMessage = message.ToLower();

        if (lowerMessage.Contains("security") || lowerMessage.Contains("injection") || lowerMessage.Contains("xss"))
            return "Security";
        if (lowerMessage.Contains("performance") || lowerMessage.Contains("slow") || lowerMessage.Contains("optimization"))
            return "Performance";
        if (lowerMessage.Contains("style") || lowerMessage.Contains("format") || lowerMessage.Contains("indentation"))
            return "Style";

        return "General";
    }
}
