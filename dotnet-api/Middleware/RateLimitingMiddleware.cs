using System.Net;
using System.Text.Json;
using System.Collections.Concurrent;

namespace dotnet_api.Middleware;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly ConcurrentDictionary<string, RateLimitInfo> _rateLimitStore = new();
    private readonly int _maxRequestsPerMinute;
    private readonly int _maxRequestsPerHour;

    public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger, IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _maxRequestsPerMinute = configuration.GetValue<int>("RateLimiting:RequestsPerMinute", 60);
        _maxRequestsPerHour = configuration.GetValue<int>("RateLimiting:RequestsPerHour", 1000);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip rate limiting for health checks
        if (context.Request.Path.StartsWithSegments("/api/health") || 
            context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }

        var clientId = GetClientIdentifier(context);
        var now = DateTime.UtcNow;

        // Clean up old entries (older than 1 hour) - do this periodically, not on every request
        if (_rateLimitStore.Count > 1000)
        {
            var keysToRemove = _rateLimitStore
                .Where(kvp => (now - kvp.Value.LastRequest).TotalHours > 1)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var key in keysToRemove)
            {
                _rateLimitStore.TryRemove(key, out _);
            }
        }

        // Get or create rate limit info
        var rateLimitInfo = _rateLimitStore.GetOrAdd(clientId, _ => new RateLimitInfo
        {
            Requests = new List<DateTime>(),
            LastRequest = now
        });

        // Thread-safe update
        lock (rateLimitInfo)
        {
            // Remove requests older than 1 minute
            rateLimitInfo.Requests.RemoveAll(r => (now - r).TotalMinutes > 1);

            // Check per-minute limit
            if (rateLimitInfo.Requests.Count >= _maxRequestsPerMinute)
            {
                _logger.LogWarning("Rate limit exceeded for client {ClientId}. Requests in last minute: {Count}", 
                    clientId, rateLimitInfo.Requests.Count);
                
                context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                context.Response.ContentType = "application/json";
                
                var response = new
                {
                    error = "Rate limit exceeded",
                    message = $"Maximum {_maxRequestsPerMinute} requests per minute allowed. Please try again later.",
                    retryAfter = 60
                };
                
                context.Response.WriteAsync(JsonSerializer.Serialize(response)).Wait();
                return;
            }

            // Check per-hour limit
            var requestsInLastHour = rateLimitInfo.Requests.Count(r => (now - r).TotalHours <= 1);
            if (requestsInLastHour >= _maxRequestsPerHour)
            {
                _logger.LogWarning("Hourly rate limit exceeded for client {ClientId}. Requests in last hour: {Count}", 
                    clientId, requestsInLastHour);
                
                context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                context.Response.ContentType = "application/json";
                
                var response = new
                {
                    error = "Rate limit exceeded",
                    message = $"Maximum {_maxRequestsPerHour} requests per hour allowed. Please try again later.",
                    retryAfter = 3600
                };
                
                context.Response.WriteAsync(JsonSerializer.Serialize(response)).Wait();
                return;
            }

            // Record this request
            rateLimitInfo.Requests.Add(now);
            rateLimitInfo.LastRequest = now;
        }

        await _next(context);
    }

    private string GetClientIdentifier(HttpContext context)
    {
        // Use IP address as primary identifier
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        
        // If authenticated, include user ID for per-user rate limiting
        var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            return $"{ipAddress}:user:{userId}";
        }
        
        return ipAddress;
    }

    private class RateLimitInfo
    {
        public List<DateTime> Requests { get; set; } = new();
        public DateTime LastRequest { get; set; }
    }
}
