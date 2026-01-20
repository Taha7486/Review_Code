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
            var correlationId = HttpContext.Items["CorrelationId"]?.ToString();
            return StatusCode(500, new dotnet_api.Models.DTOs.ErrorResponseDto
            {
                Code = dotnet_api.Models.DTOs.ErrorCodes.InternalServerError,
                Message = "An error occurred while fetching metrics.",
                Details = ex.Message,
                CorrelationId = correlationId
            });
        }
    }
}
