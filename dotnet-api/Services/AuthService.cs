using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;
using dotnet_api.Data;
using dotnet_api.Models;
using dotnet_api.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace dotnet_api.Services;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthService(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterDto registerDto)
    {
        // Check if user exists
        if (await _context.Users.AnyAsync(u => u.Email == registerDto.Email))
        {
            throw new Exception("User with this email already exists.");
        }

        // Create user
        var user = new User
        {
            Username = registerDto.Name,
            Email = registerDto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password),
            CreatedAt = DateTime.UtcNow,
            GithubAccessToken = registerDto.GithubToken
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return new AuthResponseDto
        {
            Token = CreateToken(user),
            RefreshToken = "" // Implement refresh token if needed
        };
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto loginDto)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == loginDto.Email);

        if (user == null)
        {
            throw new Exception("User not found.");
        }

        if (!BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
        {
            throw new Exception("Invalid password.");
        }

        return new AuthResponseDto
        {
            Token = CreateToken(user),
            RefreshToken = ""
        };
    }

    private string CreateToken(User user)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddDays(1);
        
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), // Numeric user ID for GetUserId()
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), // Unique token ID
            new Claim(JwtRegisteredClaimNames.Iat, ((DateTimeOffset)now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64), // Issued at
            new Claim(JwtRegisteredClaimNames.Exp, ((DateTimeOffset)expires).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64) // Expiration
        };

        var rawKey = _configuration.GetSection("JwtSettings:Key").Value;
        var expandedKey = ExpandEnvVars(rawKey) ?? "super_secret_key_that_is_long_enough_for_hmac_sha256";

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(expandedKey));

        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

        var token = new JwtSecurityToken(
            claims: claims,
            expires: expires,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string? ExpandEnvVars(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return System.Text.RegularExpressions.Regex.Replace(value, @"\$\{(\w+):([^}]*)\}", match =>
        {
            var envVar = match.Groups[1].Value;
            var defaultValue = match.Groups[2].Value;
            return Environment.GetEnvironmentVariable(envVar) ?? defaultValue;
        });
    }
}