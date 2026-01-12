using System.Text;
using System.Text.RegularExpressions;

namespace dotnet_api.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Items["CorrelationId"]?.ToString() ?? "unknown";
        var request = context.Request;
        var method = request.Method;
        var path = request.Path;
        var query = request.QueryString;

        // Log Request Start (Debug level to reduce production noise)
        _logger.LogDebug("[{CorrelationId}] Request Started: {Method} {Path}{Query}", correlationId, method, path, query);
        
        // Optionally enable this for debugging body content, but careful with large payloads
        // await LogRequestBody(request, correlationId);

        var startTime = DateTime.UtcNow;

        try
        {
            await _next(context);
        }
        finally
        {
            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
            var statusCode = context.Response.StatusCode;

            // Log Request End
            _logger.LogInformation("[{CorrelationId}] Request Completed: {Method} {Path} - Status: {StatusCode} in {Duration}ms",
                correlationId, method, path, statusCode, duration);
        }
    }

    // Helper to read and log request body with redaction (only calls enabling stream buffering)
    // currently unused to save perf, but could be enabled for debugging
    private async Task LogRequestBody(HttpRequest request, string correlationId)
    {
        request.EnableBuffering();
        
        using var reader = new StreamReader(request.Body, Encoding.UTF8, true, 1024, true);
        var body = await reader.ReadToEndAsync();
        
        // Reset position for next middleware
        request.Body.Position = 0;

        if (!string.IsNullOrEmpty(body))
        {
            var redactedBody = RedactSensitiveData(body);
            // Truncate logs if too long
            if (redactedBody.Length > 2000) redactedBody = redactedBody.Substring(0, 2000) + "... [Truncated]";
            
            _logger.LogInformation("[{CorrelationId}] Request Body: {Body}", correlationId, redactedBody);
        }
    }
    
    private string RedactSensitiveData(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        
        // Redact potential tokens (40+ character alphanumeric strings)
        input = Regex.Replace(input, @"\b[a-zA-Z0-9]{40,}\b", "[REDACTED]");
        
        // Redact common sensitive JSON fields or patterns
        input = Regex.Replace(input, @"(token|key|secret|password|authorization)\s*[:=]\s*[^\s,}\""]+", 
            match => $"{match.Groups[1].Value}: [REDACTED]", RegexOptions.IgnoreCase);
            
        // Redact JSON content (simplified regex for "field": "value")
        input = Regex.Replace(input, @"""(token|password|client_secret|access_token|refresh_token)""\s*:\s*""[^""]+""", 
            @"""$1"": ""[REDACTED]""", RegexOptions.IgnoreCase);

        return input;
    }
}
