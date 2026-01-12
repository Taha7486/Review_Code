using Microsoft.EntityFrameworkCore;
using dotnet_api.Data;
using dotnet_api.Services;
using dotnet_api.Services.Helpers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

// Load environment variables from .env file if it exists (for local development)
var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (!File.Exists(envPath))
{
    // Try looking in the project root (one level up)
    envPath = Path.Combine(Directory.GetCurrentDirectory(), "..", ".env");
}

// Note: Logger is not available yet at this point, so we'll log after builder is created
var envFileLoaded = false;
if (File.Exists(envPath))
{
    foreach (var line in File.ReadAllLines(envPath))
    {
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
        var parts = line.Split('=', 2);
        if (parts.Length == 2)
        {
            Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
        }
    }
    envFileLoaded = true;
}

// Helper function to expand environment variables in config values
string ExpandEnvVars(string? value)
{
    if (string.IsNullOrEmpty(value)) return value ?? "";
    return Regex.Replace(value, @"\$\{(\w+):([^}]*)\}", match =>
    {
        var envVar = match.Groups[1].Value;
        var defaultValue = match.Groups[2].Value;
        return Environment.GetEnvironmentVariable(envVar) ?? defaultValue;
    });
}

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Use camelCase for JSON serialization to match JavaScript conventions
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.WriteIndented = false;
    });
builder.Services.AddHttpClient(); // 🔌 Essential for stable external API calls
builder.Services.AddMemoryCache(); // 🚀 Enable server-side caching for expensive operations

// Add CORS (Environment-driven)
var allowedOrigins = ExpandEnvVars(builder.Configuration.GetSection("Cors:AllowedOrigins").Value ?? "http://localhost:3000")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy =>
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});

// Add Entity Framework Core with MySQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseMySql(
        ExpandEnvVars(builder.Configuration.GetConnectionString("DefaultConnection")),
        new MySqlServerVersion(new Version(8, 0, 0))
    );
    
    // CRITICAL FIX: Disable change tracking by default to prevent race conditions
    // This ensures queries always fetch fresh data from database instead of cached entities
    // Tracking can be enabled per-query with .AsTracking() when needed for updates
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
});

// Register Auth Service
builder.Services.AddScoped<IAuthService, AuthService>();

// Register Helper Services
builder.Services.AddScoped<IGitHubClientService, GitHubClientService>();
builder.Services.AddScoped<IPhpServiceClient, PhpServiceClient>();
builder.Services.AddScoped<IDataSanitizer, DataSanitizer>();
builder.Services.AddScoped<IFileFilter, FileFilter>();
builder.Services.AddScoped<IGitHubFileService, GitHubFileService>();
builder.Services.AddScoped<IRepositoryService, RepositoryService>();
builder.Services.AddScoped<IMetricsCalculator, MetricsCalculator>();

// Register Analysis Service
builder.Services.AddScoped<IAnalysisService, AnalysisService>();

// Register Background Analysis Processor
builder.Services.AddSingleton<BackgroundAnalysisProcessor>();
builder.Services.AddHostedService<BackgroundAnalysisProcessor>(sp => sp.GetRequiredService<BackgroundAnalysisProcessor>());

// Register Metrics Service
builder.Services.AddSingleton<IMetricsService, MetricsService>();

// Configure JWT Authentication
var jwtKey = ExpandEnvVars(builder.Configuration.GetSection("JwtSettings:Key").Value);
if (string.IsNullOrEmpty(jwtKey) || jwtKey == "change_me_in_production" || jwtKey == "super_secret_key_that_is_long_enough_for_hmac_sha256")
{
    // In production, we MUST have a secure key.
    if (builder.Environment.IsProduction())
    {
        throw new InvalidOperationException("CRITICAL: JWT_SECRET_KEY is not set or is using a development default. The application cannot start in Production mode without a secure key.");
    }
    jwtKey ??= "super_secret_key_that_is_long_enough_for_hmac_sha256";
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateIssuer = false,
        ValidateAudience = false
    };
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Log environment variable loading status now that logger is available
var logger = app.Services.GetRequiredService<ILogger<Program>>();
if (envFileLoaded)
{
    logger.LogInformation("Environment variables loaded from: {EnvPath}", envPath);
}
else if (app.Environment.IsDevelopment())
{
    logger.LogWarning("No .env file found at {EnvPath}. Ensure environment variables are set manually.", envPath);
}

// Configure the HTTP request pipeline.
app.UseMiddleware<dotnet_api.Middleware.CorrelationIdMiddleware>();
app.UseMiddleware<dotnet_api.Middleware.RequestLoggingMiddleware>();
app.UseMiddleware<dotnet_api.Middleware.RateLimitingMiddleware>();
app.UseMiddleware<dotnet_api.Middleware.GlobalExceptionHandler>();
app.MapOpenApi();
app.UseCors("AllowReactApp"); // Enable CORS
app.UseAuthentication(); // Add this
app.UseAuthorization();  // Add this
app.MapControllers();

//app.UseHttpsRedirection();




app.Run();


