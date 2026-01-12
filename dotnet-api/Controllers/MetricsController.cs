using dotnet_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dotnet_api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class MetricsController : ControllerBase
{
    private readonly IMetricsService _metricsService;

    public MetricsController(IMetricsService metricsService)
    {
        _metricsService = metricsService;
    }

    // GET: api/metrics
    [HttpGet]
    public async Task<IActionResult> GetMetrics([FromQuery] DateTime? since = null)
    {
        try
        {
            var metrics = await _metricsService.GetMetricsAsync(since);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }
}
