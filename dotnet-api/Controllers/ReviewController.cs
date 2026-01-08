using dotnet_api.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dotnet_api.Controllers;

[Authorize] // 🔒 Ensures only logged-in users can access this
[ApiController]
[Route("api/[controller]")]
public class ReviewController : ControllerBase
{
    private readonly IReviewService _reviewService;

    // The Dependency Injection system gives us the Service automatically
    public ReviewController(IReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    // POST: api/review/analyze
    [HttpPost("analyze")]
    public async Task<IActionResult> AnalyzePullRequest([FromBody] AnalyzePrDto request)
    {
        // 1. Controller receives the request
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            // 2. Controller delegates the "Heavy Lifting" to the Service
            var result = await _reviewService.AnalyzePullRequestAsync(request);
            
            // 3. Return the result to React
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }
}
