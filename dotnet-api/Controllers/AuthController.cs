using dotnet_api.Models.DTOs;
using dotnet_api.Services;
using Microsoft.AspNetCore.Mvc;

namespace dotnet_api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    private ErrorResponseDto CreateErrorResponse(string code, string message, object? details = null)
    {
        var correlationId = HttpContext.Items["CorrelationId"]?.ToString();
        return new ErrorResponseDto
        {
            Code = code,
            Message = message,
            Details = details,
            CorrelationId = correlationId
        };
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> RegisterAsync([FromBody] RegisterDto registerDto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>()
                );
            return BadRequest(CreateErrorResponse(
                ErrorCodes.ValidationError,
                "Registration validation failed",
                errors
            ));
        }

        try
        {
            var result = await _authService.RegisterAsync(registerDto);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists") || ex.Message.Contains("email"))
        {
            _logger.LogWarning(ex, "Registration failed: user already exists");
            return Conflict(CreateErrorResponse(
                ErrorCodes.ValidationError,
                "A user with this email already exists"
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during registration");
            var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
            return BadRequest(CreateErrorResponse(
                ErrorCodes.ValidationError,
                "Registration failed. Please check your input and try again.",
                isDevelopment ? ex.Message : null
            ));
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> LoginAsync([FromBody] LoginDto loginDto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>()
                );
            return BadRequest(CreateErrorResponse(
                ErrorCodes.ValidationError,
                "Login validation failed",
                errors
            ));
        }

        try
        {
            var result = await _authService.LoginAsync(loginDto);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Login failed: invalid credentials");
            return Unauthorized(CreateErrorResponse(
                ErrorCodes.Unauthorized,
                "Invalid email or password"
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login");
            var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
            return BadRequest(CreateErrorResponse(
                ErrorCodes.ValidationError,
                "Login failed. Please check your credentials and try again.",
                isDevelopment ? ex.Message : null
            ));
        }
    }
}