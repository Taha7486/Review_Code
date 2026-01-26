using System.Text.Json;
using dotnet_api.Models;

namespace dotnet_api.Services.Helpers;

/// <summary>
/// Handles communication with the PHP analysis service
/// </summary>
public interface IPhpServiceClient
{
    Task<PhpAnalysisResponse> AnalyzeFilesAsync(List<PullRequestFileDetail> files, string correlationId);
}

public class PhpServiceClient : IPhpServiceClient
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PhpServiceClient> _logger;
    private readonly IDataSanitizer _dataSanitizer;
    private readonly IPrometheusMetricsService _prometheusMetrics;

    public PhpServiceClient(
        IConfiguration config,
        IHttpClientFactory httpClientFactory,
        ILogger<PhpServiceClient> logger,
        IDataSanitizer dataSanitizer,
        IPrometheusMetricsService prometheusMetrics)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _dataSanitizer = dataSanitizer;
        _prometheusMetrics = prometheusMetrics;
    }

    public async Task<PhpAnalysisResponse> AnalyzeFilesAsync(List<PullRequestFileDetail> files, string correlationId)
    {
        _logger.LogDebug("[{CorrelationId}] Sending {FileCount} files to PHP analysis service", correlationId, files.Count);

        var rawUrl = _config["ServiceUrls:PhpAnalysisApi"];
        _logger.LogDebug("[{CorrelationId}] Raw config value: {RawUrl}", correlationId, rawUrl ?? "NULL");
        
        var phpServiceUrl = ExpandEnvVars(rawUrl);
        
        if (string.IsNullOrWhiteSpace(phpServiceUrl))
        {
            _logger.LogError("[{CorrelationId}] PHP service URL is not configured. Check appsettings.json and environment variables.", correlationId);
            throw new InvalidOperationException("PHP_ANALYSIS_API_URL is not configured. Please set it in .env file or appsettings.json");
        }

        // Validate that we have an absolute URL
        if (!Uri.TryCreate(phpServiceUrl, UriKind.Absolute, out var uri))
        {
            _logger.LogError("[{CorrelationId}] Invalid PHP service URL: {Url}", correlationId, phpServiceUrl);
            throw new InvalidOperationException($"Invalid PHP service URL: {phpServiceUrl}. Must be an absolute URL like http://localhost:8000/api/analyze/files");
        }

        _logger.LogDebug("[{CorrelationId}] Connecting to PHP Service at: {PhpServiceUrl}", correlationId, phpServiceUrl);

        var payload = new 
        { 
            files = files.Select(f => new { path = f.FileName, content = f.Content }).ToList()
        };
        var jsonContent = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(10); // Match PHP's 600s limit & Background Processor
        client.DefaultRequestHeaders.Add("X-Correlation-Id", correlationId);
        
        // Add shared secret for service-to-service authentication
        var internalSecret = _config["ServiceUrls:InternalSecret"];
        if (!string.IsNullOrEmpty(internalSecret) && internalSecret != "change_me_in_production")
        {
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", internalSecret);
        }
        
        HttpResponseMessage? response = null;
        try
        {
            response = await client.PostAsync(phpServiceUrl, jsonContent);
            
            if (!response.IsSuccessStatusCode)
            {
                _prometheusMetrics.IncrementPhpServiceCall("failure", "http_error");
                var errorBody = await response.Content.ReadAsStringAsync();
                var sanitizedError = _dataSanitizer.RedactSensitiveData(errorBody.Length > 200 ? errorBody.Substring(0, 200) + "..." : errorBody);
                _logger.LogError("[{CorrelationId}] PHP service returned {StatusCode}. Error: {Error}", correlationId, response.StatusCode, sanitizedError);
                throw new HttpRequestException($"PHP analysis service returned status {response.StatusCode}. Body: {sanitizedError}");
            }
        }
        catch (TaskCanceledException)
        {
            _prometheusMetrics.IncrementPhpServiceCall("failure", "timeout");
            _logger.LogError("[{CorrelationId}] PHP service request timed out after {Timeout} minutes", correlationId, client.Timeout.TotalMinutes);
            throw;
        }
        catch (HttpRequestException ex)
        {
            _prometheusMetrics.IncrementPhpServiceCall("failure", "connection");
            _logger.LogError(ex, "[{CorrelationId}] Failed to connect to PHP service at {Url}", correlationId, phpServiceUrl);
            throw;
        }

        var content = await response.Content.ReadAsStringAsync();
        PhpAnalysisResponse? phpResult = null;
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            phpResult = JsonSerializer.Deserialize<PhpAnalysisResponse>(content, options);
        }
        catch (JsonException)
        {
            _prometheusMetrics.IncrementPhpServiceCall("failure", "parse");
            _logger.LogError("[{CorrelationId}] Failed to parse PHP service response", correlationId);
            throw;
        }

        if (phpResult == null)
        {
            _prometheusMetrics.IncrementPhpServiceCall("failure", "empty_response");
            _logger.LogError("[{CorrelationId}] Empty response from PHP service", correlationId);
            throw new Exception("Empty response from PHP service");
        }

        // Success!
        _prometheusMetrics.IncrementPhpServiceCall("success", "none");
        
        _logger.LogDebug(
            "[{CorrelationId}] PHP analysis complete. Issues: {IssueCount}, Score: {Score}",
            correlationId, phpResult.TotalIssues, phpResult.AverageScore);

        return phpResult;
    }

    /// <summary>
    /// Expands environment variables in the format ${VAR_NAME:default_value}
    /// This matches the implementation in Program.cs
    /// </summary>
    private string? ExpandEnvVars(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;
        
        // Use regex to replace ${VAR:default} with environment variable or default
        return System.Text.RegularExpressions.Regex.Replace(input, @"\$\{(\w+):([^}]*)\}", match =>
        {
            var envVar = match.Groups[1].Value;
            var defaultValue = match.Groups[2].Value;
            var value = Environment.GetEnvironmentVariable(envVar);
            
            if (string.IsNullOrEmpty(value))
            {
                _logger.LogDebug("Environment variable {EnvVar} not found, using default: {Default}", envVar, defaultValue);
                return defaultValue;
            }
            
            _logger.LogDebug("Environment variable {EnvVar} found: {Value}", envVar, value);
            return value;
        });
    }
}

// Helper class for file details
public class PullRequestFileDetail
{
    public string FileName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

// PHP Response Classes
public class PhpAnalysisResponse
{
    public int FilesAnalyzed { get; set; }
    public double AverageScore { get; set; }
    public int TotalIssues { get; set; }
    public List<PhpFileAnalysis> Results { get; set; } = new();
}

public class PhpFileAnalysis
{
    [System.Text.Json.Serialization.JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;
    
    // Fallback for older PascalCase or simpler "File"
    public string File { get; set; } = string.Empty;
    
    public double Score { get; set; }
    public List<PhpIssue> Issues { get; set; } = new();
}

public class PhpIssue
{
    [System.Text.Json.Serialization.JsonPropertyName("lineNumber")]
    public int LineNumber { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("line")]
    public int Line { get; set; }
    
    public string Severity { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
