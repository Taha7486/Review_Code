namespace dotnet_api.Middleware;

public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private const string CorrelationIdHeader = "X-Correlation-ID";

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Get or create correlation ID
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault() 
            ?? Guid.NewGuid().ToString("N")[..8];

        // Add to response headers
        context.Response.Headers[CorrelationIdHeader] = correlationId;

        // Store in HttpContext.Items for use in services
        context.Items["CorrelationId"] = correlationId;

        await _next(context);
    }
}
