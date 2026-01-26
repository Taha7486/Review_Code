using Prometheus;

namespace dotnet_api.Services
{
    /// <summary>
    /// Service for collecting and exposing Prometheus metrics
    /// Tracks business-specific metrics for the code review application
    /// </summary>
    public class PrometheusMetricsService : IPrometheusMetricsService
    {
        // Counter: Total analyses started (never decreases)
        private static readonly Counter AnalysesStarted = Metrics
            .CreateCounter(
                "code_review_analyses_started_total",
                "Total number of code analyses started",
                new CounterConfiguration
                {
                    LabelNames = new[] { "status" } // started, completed, failed
                });

        // Gauge: Currently running analyses (goes up and down)
        private static readonly Gauge ActiveAnalyses = Metrics
            .CreateGauge(
                "code_review_analyses_active",
                "Number of analyses currently in progress");

        // Histogram: Analysis duration (for percentiles and averages)
        private static readonly Histogram AnalysisDuration = Metrics
            .CreateHistogram(
                "code_review_analysis_duration_seconds",
                "Time taken to complete a full analysis",
                new HistogramConfiguration
                {
                    // Buckets: 5s, 10s, 30s, 1m, 2m, 5m, 10m, 30m
                    Buckets = new[] { 5.0, 10.0, 30.0, 60.0, 120.0, 300.0, 600.0, 1800.0 }
                });

        // Counter: Issues found by severity and category
        private static readonly Counter IssuesFound = Metrics
            .CreateCounter(
                "code_review_issues_found_total",
                "Total issues detected by analysis engine",
                new CounterConfiguration
                {
                    LabelNames = new[] { "severity", "category" } // critical/high/medium/low, security/complexity/style
                });

        // Gauge: GitHub API rate limit remaining
        private static readonly Gauge GitHubRateLimit = Metrics
            .CreateGauge(
                "github_api_rate_limit_remaining",
                "Remaining GitHub API calls before rate limit");

        // Gauge: GitHub API rate limit maximum
        private static readonly Gauge GitHubRateLimitMax = Metrics
            .CreateGauge(
                "github_api_rate_limit_max",
                "Maximum GitHub API calls per hour");

        // Counter: PHP service calls
        private static readonly Counter PhpServiceCalls = Metrics
            .CreateCounter(
                "php_service_calls_total",
                "Total calls to PHP analysis service",
                new CounterConfiguration
                {
                    LabelNames = new[] { "status", "error_type" } // success/failure, timeout/connection/parse/none
                });

        // Histogram: Database query duration
        private static readonly Histogram DatabaseQueryDuration = Metrics
            .CreateHistogram(
                "database_query_duration_milliseconds",
                "Database query execution time",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "operation" }, // select/insert/update/delete
                    Buckets = new[] { 10.0, 50.0, 100.0, 250.0, 500.0, 1000.0, 2500.0, 5000.0 } // milliseconds
                });

        /// <summary>
        /// Increment analysis started counter
        /// </summary>
        public void IncrementAnalysisStarted(string status)
        {
            AnalysesStarted.WithLabels(status).Inc();
        }

        /// <summary>
        /// Increment active analysis gauge
        /// </summary>
        public void IncrementActiveAnalysis()
        {
            ActiveAnalyses.Inc();
        }

        /// <summary>
        /// Decrement active analysis gauge
        /// </summary>
        public void DecrementActiveAnalysis()
        {
            ActiveAnalyses.Dec();
        }

        /// <summary>
        /// Record analysis duration
        /// </summary>
        public void RecordAnalysisDuration(double durationSeconds)
        {
            AnalysisDuration.Observe(durationSeconds);
        }

        /// <summary>
        /// Increment issues found counter
        /// </summary>
        public void IncrementIssuesFound(string severity, string category)
        {
            IssuesFound.WithLabels(severity.ToLower(), category.ToLower()).Inc();
        }

        /// <summary>
        /// Record GitHub API rate limit
        /// </summary>
        public void RecordGitHubRateLimit(int remaining, int limit)
        {
            GitHubRateLimit.Set(remaining);
            GitHubRateLimitMax.Set(limit);
        }

        /// <summary>
        /// Increment PHP service call counter
        /// </summary>
        public void IncrementPhpServiceCall(string status, string errorType = "none")
        {
            PhpServiceCalls.WithLabels(status, errorType).Inc();
        }

        /// <summary>
        /// Record database query duration
        /// </summary>
        public void RecordDatabaseQueryDuration(double durationMs, string operation)
        {
            DatabaseQueryDuration.WithLabels(operation).Observe(durationMs);
        }
    }
}
