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
        /// Increment issues found counter by severity and category
        /// </summary>
        void IncrementIssuesFound(string severity, string category);
    }
}
