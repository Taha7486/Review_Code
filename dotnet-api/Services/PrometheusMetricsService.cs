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

        // Counter: Files processed by the PHP analysis engine
        private static readonly Counter FilesAnalyzedCounter = Metrics
            .CreateCounter(
                "php_files_analyzed_total",
                "Total files processed by the PHP analysis engine");

        // Histogram: End-to-end analysis duration in seconds
        private static readonly Histogram AnalysisDurationHistogram = Metrics
            .CreateHistogram(
                "code_review_analysis_duration_seconds",
                "End-to-end duration of a code analysis run",
                new HistogramConfiguration
                {
                    Buckets = Histogram.ExponentialBuckets(1, 2, 8) // 1s,2s,4s,8s,16s,32s,64s,128s
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

        public void RecordFilesAnalyzed(int count)
        {
            FilesAnalyzedCounter.Inc(count);
        }

        public void RecordAnalysisDuration(double seconds)
        {
            AnalysisDurationHistogram.Observe(seconds);
        }
    }
}
