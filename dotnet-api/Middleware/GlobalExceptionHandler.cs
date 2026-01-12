using System.Net;
using System.Text.Json;
using dotnet_api.Models.DTOs;

namespace dotnet_api.Middleware;

public class GlobalExceptionHandler
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(RequestDelegate next, ILogger<GlobalExceptionHandler> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var correlationId = context.Items["CorrelationId"]?.ToString() ?? "unknown";
            
            // Log full exception details internally
            _logger.LogError(ex, 
                "[{CorrelationId}] Unhandled exception at {Path} ({Method}): {Message}",
                correlationId, context.Request.Path, context.Request.Method, ex.Message);

            await HandleExceptionAsync(context, ex, correlationId);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception, string correlationId)
    {
        context.Response.ContentType = "application/json";
        
        var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
        
        var statusCode = (int)HttpStatusCode.InternalServerError;
        var errorCode = ErrorCodes.InternalServerError;
        var message = "An unexpected error occurred on the server.";

        // Specific handling for known exceptions
        if (exception is ArgumentException || exception is ArgumentNullException || exception is InvalidOperationException)
        {
            statusCode = (int)HttpStatusCode.BadRequest;
            errorCode = ErrorCodes.InvalidRequest;
            message = exception.Message;
        }
        else if (exception is UnauthorizedAccessException || exception.Message.Contains("Unauthorized"))
        {
            statusCode = (int)HttpStatusCode.Unauthorized;
            errorCode = ErrorCodes.Unauthorized;
            message = "You are not authorized to perform this action.";
        }
        else if (exception is Octokit.NotFoundException || exception is KeyNotFoundException)
        {
            statusCode = (int)HttpStatusCode.NotFound;
            errorCode = ErrorCodes.NotFound;
            message = "The requested resource was not found.";
        }

        context.Response.StatusCode = statusCode;

        var response = new ErrorResponseDto
        {
            Code = errorCode,
            Message = isDevelopment ? exception.Message : message,
            CorrelationId = correlationId,
            Details = isDevelopment ? exception.StackTrace : null
        };

        var jsonOptions = new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = isDevelopment 
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response, jsonOptions));
    }
}
