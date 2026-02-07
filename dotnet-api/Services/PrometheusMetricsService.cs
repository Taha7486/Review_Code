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

        // Counter: Issues found by severity and category
        private static readonly Counter IssuesFound = Metrics
            .CreateCounter(
                "code_review_issues_found_total",
                "Total issues detected by analysis engine",
                new CounterConfiguration
                {
                    LabelNames = new[] { "severity", "category" } // critical/high/medium/low, security/complexity/style
                });

        /// <summary>
        /// Increment analysis started counter
        /// </summary>
        public void IncrementAnalysisStarted(string status)
        {
            AnalysesStarted.WithLabels(status).Inc();
        }

        /// <summary>
        /// Increment issues found counter
        /// </summary>
        public void IncrementIssuesFound(string severity, string category)
        {
            IssuesFound.WithLabels(severity.ToLower(), category.ToLower()).Inc();
        }
    }
}
