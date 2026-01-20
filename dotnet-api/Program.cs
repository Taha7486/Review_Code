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
        new MySqlServerVersion(new Version(8, 0, 0)),
        mysqlOptions => mysqlOptions.CommandTimeout(180) // ⏳ Increase timeout to 3 minutes for large saves
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

// Register Health Checks
builder.Services.AddHealthChecks();

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

// Configure Swagger/OpenAPI documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Code Review Automation API",
        Version = "v1",
        Description = "RESTful API for automated code review and analysis. Analyzes GitHub repositories for code quality, security vulnerabilities, and best practices.",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "Code Review Tool",
            Url = new Uri("https://github.com/yourusername/code-review-tool")
        }
    });

    // Add JWT Authentication support to Swagger UI
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below. Example: 'Bearer eyJhbGciOiJIUzI1NiIs...'",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

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
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});

// Run Startup Validation
try 
{
    dotnet_api.Configuration.StartupConfigValidation.Validate(app.Configuration, logger);
}
catch (Exception ex)
{
    // Ensure we log and stop if validation fails
    logger.LogCritical(ex, "Application start-up failed due to configuration errors.");
    throw;
}

app.UseMiddleware<dotnet_api.Middleware.CorrelationIdMiddleware>();
app.UseMiddleware<dotnet_api.Middleware.RequestLoggingMiddleware>();
app.UseMiddleware<dotnet_api.Middleware.RateLimitingMiddleware>();
app.UseMiddleware<dotnet_api.Middleware.GlobalExceptionHandler>();

// Enable Swagger UI (accessible at /swagger)
if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "Staging")
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Code Review API v1");
        options.RoutePrefix = "swagger"; // Access Swagger UI at /swagger
        options.DocumentTitle = "Code Review API Documentation";
        options.DefaultModelsExpandDepth(2);
        options.DefaultModelExpandDepth(2);
        options.DisplayRequestDuration();
        options.EnableTryItOutByDefault();
    });
    
    logger.LogInformation("📚 Swagger UI available at: http://localhost:5116/swagger");
}

app.MapOpenApi();
app.UseCors("AllowReactApp"); 
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health"); // 🏥 Add Health Check Endpoint

app.Run();

// Make the Program class accessible to integration tests
public partial class Program { }
