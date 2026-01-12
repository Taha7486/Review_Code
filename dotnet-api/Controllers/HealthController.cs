using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using dotnet_api.Data;
using System.Text.RegularExpressions;

namespace dotnet_api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<HealthController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public HealthController(
        ApplicationDbContext context, 
        ILogger<HealthController> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    // GET: api/health
    [HttpGet]
    public async Task<IActionResult> GetHealth()
    {
        var dbCheck = await CheckDatabaseAsync();
        var memoryCheck = CheckMemory();
        
        var health = new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            version = "1.0.0",
            checks = new
            {
                database = dbCheck,
                memory = memoryCheck
            }
        };

        var dbStatus = ((dynamic)dbCheck).status?.ToString() ?? "unknown";
        var memStatus = ((dynamic)memoryCheck).status?.ToString() ?? "unknown";
        var isHealthy = dbStatus == "healthy" && memStatus == "healthy";

        return isHealthy 
            ? Ok(health) 
            : StatusCode(503, health);
    }

    // GET: api/health/ready
    [HttpGet("ready")]
    public async Task<IActionResult> GetReadiness()
    {
        var dbCheck = await CheckDatabaseAsync();
        var dbStatus = ((dynamic)dbCheck).status?.ToString() ?? "unknown";
        var dbMessage = ((dynamic)dbCheck).message?.ToString() ?? "";
        
        if (dbStatus == "healthy")
        {
            return Ok(new { status = "ready", timestamp = DateTime.UtcNow });
        }
        
        return StatusCode(503, new { status = "not ready", reason = dbMessage });
    }

    // GET: api/health/live
    [HttpGet("live")]
    public IActionResult GetLiveness()
    {
        return Ok(new { status = "alive", timestamp = DateTime.UtcNow });
    }

    // GET: /healthz (lightweight health check for readiness probes)
    [HttpGet("/healthz")]
    public async Task<IActionResult> GetHealthz()
    {
        var dbCheck = await CheckDatabaseAsync();
        var phpCheck = await CheckPhpServiceAsync();
        
        var dbStatus = ((dynamic)dbCheck).status?.ToString() ?? "unknown";
        var phpStatus = ((dynamic)phpCheck).status?.ToString() ?? "unknown";
        
        var isHealthy = dbStatus == "healthy" && phpStatus == "healthy";
        
        var response = new
        {
            status = isHealthy ? "healthy" : "unhealthy",
            timestamp = DateTime.UtcNow,
            checks = new
            {
                database = dbCheck,
                phpService = phpCheck
            }
        };

        return isHealthy ? Ok(response) : StatusCode(503, response);
    }

    private async Task<object> CheckDatabaseAsync()
    {
        try
        {
            var canConnect = await _context.Database.CanConnectAsync();
            if (canConnect)
            {
                // Try a simple query
                await _context.Users.CountAsync();
                return new { status = "healthy", message = "Database connection successful" };
            }
            return new { status = "unhealthy", message = "Cannot connect to database" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return new { status = "unhealthy", message = $"Database error: {ex.Message}" };
        }
    }

    private object CheckMemory()
    {
        try
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var memoryMB = process.WorkingSet64 / (1024 * 1024);
            var maxMemoryMB = 500; // Alert if over 500MB
            
            if (memoryMB > maxMemoryMB)
            {
                return new { status = "degraded", message = $"High memory usage: {memoryMB}MB" };
            }
            
            return new { status = "healthy", message = $"Memory usage: {memoryMB}MB" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Memory check failed");
            return new { status = "unknown", message = $"Memory check error: {ex.Message}" };
        }
    }

    private async Task<object> CheckPhpServiceAsync()
    {
        try
        {
            var phpServiceUrl = _configuration.GetSection("ServiceUrls:PhpAnalysisApi").Value;
            if (string.IsNullOrEmpty(phpServiceUrl))
            {
                return new { status = "unknown", message = "PHP service URL not configured" };
            }

            // Expand environment variables in URL
            phpServiceUrl = Regex.Replace(phpServiceUrl, @"\$\{(\w+):([^}]*)\}", match =>
            {
                var envVar = match.Groups[1].Value;
                var defaultValue = match.Groups[2].Value;
                return Environment.GetEnvironmentVariable(envVar) ?? defaultValue;
            });

            // Use a lightweight ping - try to connect with a short timeout
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(2);
            
            // Try to reach the PHP service (even if it returns an error, if we get a response, it's reachable)
            var response = await httpClient.GetAsync(phpServiceUrl.Replace("/api/analyze/files", "/health") ?? phpServiceUrl);
            
            // If we get any HTTP response (even 404), the service is reachable
            return new { status = "healthy", message = "PHP service is reachable" };
        }
        catch (TaskCanceledException)
        {
            return new { status = "unhealthy", message = "PHP service timeout - service not responding" };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PHP service health check failed");
            return new { status = "unhealthy", message = $"PHP service unreachable: {ex.Message}" };
        }
    }
}
