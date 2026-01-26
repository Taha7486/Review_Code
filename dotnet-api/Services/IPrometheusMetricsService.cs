namespace dotnet_api.Services
{
    /// <summary>
    /// Interface for Prometheus metrics service
    /// </summary>
    public interface IPrometheusMetricsService
    {
        /// <summary>
        /// Increment analysis started counter by status
        /// </summary>
        void IncrementAnalysisStarted(string status);

        /// <summary>
        /// Increment active analysis gauge
        /// </summary>
        void IncrementActiveAnalysis();

        /// <summary>
        /// Decrement active analysis gauge
        /// </summary>
        void DecrementActiveAnalysis();

        /// <summary>
        /// Record analysis duration in seconds
        /// </summary>
        void RecordAnalysisDuration(double durationSeconds);

        /// <summary>
        /// Increment issues found counter by severity and category
        /// </summary>
        void IncrementIssuesFound(string severity, string category);

        /// <summary>
        /// Record current GitHub API rate limit
        /// </summary>
        void RecordGitHubRateLimit(int remaining, int limit);

        /// <summary>
        /// Increment PHP service call counter
        /// </summary>
        void IncrementPhpServiceCall(string status, string errorType = "none");

        /// <summary>
        /// Record database query duration in milliseconds
        /// </summary>
        void RecordDatabaseQueryDuration(double durationMs, string operation);
    }
}
