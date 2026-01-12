namespace dotnet_api.Models.DTOs;

/// <summary>
/// Standard error response envelope for all 4xx/5xx responses.
/// Ensures consistent error structure across all API endpoints.
/// </summary>
public class ErrorResponseDto
{
    /// <summary>
    /// Human-readable error message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Machine-readable error code for programmatic handling
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Optional additional details (e.g., validation errors, stack trace in dev)
    /// </summary>
    public object? Details { get; set; }

    /// <summary>
    /// Correlation ID for tracing the request
    /// </summary>
    public string? CorrelationId { get; set; }
}

/// <summary>
/// Standard error codes used across the API
/// </summary>
public static class ErrorCodes
{
    // Validation errors (400)
    public const string ValidationError = "VALIDATION_ERROR";
    public const string InvalidRequest = "INVALID_REQUEST";
    public const string MissingRequiredField = "MISSING_REQUIRED_FIELD";

    // Authentication/Authorization errors (401, 403)
    public const string Unauthorized = "UNAUTHORIZED";
    public const string InvalidToken = "INVALID_TOKEN";
    public const string Forbidden = "FORBIDDEN";

    // Not found errors (404)
    public const string NotFound = "NOT_FOUND";
    public const string RepositoryNotFound = "REPOSITORY_NOT_FOUND";
    public const string BranchNotFound = "BRANCH_NOT_FOUND";
    public const string AnalysisRunNotFound = "ANALYSIS_RUN_NOT_FOUND";

    // Rate limiting (429)
    public const string RateLimitExceeded = "RATE_LIMIT_EXCEEDED";
    public const string GitHubRateLimitExceeded = "GITHUB_RATE_LIMIT_EXCEEDED";

    // GitHub API errors
    public const string GitHubNotFound = "GITHUB_NOT_FOUND";
    public const string GitHubUnauthorized = "GITHUB_UNAUTHORIZED";
    public const string GitHubForbidden = "GITHUB_FORBIDDEN";

    // PHP Service errors
    public const string PhpServiceError = "PHP_SERVICE_ERROR";
    public const string PhpServiceTimeout = "PHP_SERVICE_TIMEOUT";
    public const string PhpServiceUnavailable = "PHP_SERVICE_UNAVAILABLE";

    // Analysis errors
    public const string AnalysisFailed = "ANALYSIS_FAILED";
    public const string AnalysisTimeout = "ANALYSIS_TIMEOUT";
    public const string FileLimitExceeded = "FILE_LIMIT_EXCEEDED";
    public const string FileSizeExceeded = "FILE_SIZE_EXCEEDED";

    // Server errors (500)
    public const string InternalServerError = "INTERNAL_SERVER_ERROR";
    public const string DatabaseError = "DATABASE_ERROR";
}
