using System.Text;

namespace dotnet_api.Configuration;

public static class StartupConfigValidation
{
    public static void Validate(IConfiguration configuration, ILogger logger)
    {
        var missingVars = new List<string>();
        var requiredVars = new[]
        {
            "DB_HOST",
            "DB_NAME",
            "DB_USER",
            "DB_PASSWORD",
            "JWT_SECRET_KEY",
            "PHP_ANALYSIS_API_URL",
            "INTERNAL_SERVICE_SECRET"
        };

        // Map environment variable names to config paths for better error messages
        var envToConfigMap = new Dictionary<string, string>
        {
            { "PHP_ANALYSIS_API_URL", "ServiceUrls:PhpAnalysisApi" },
            { "INTERNAL_SERVICE_SECRET", "ServiceUrls:InternalSecret" }
        };

        foreach (var key in requiredVars)
        {
            // Check environment variables first (since .env loads into Environment)
            var envVal = Environment.GetEnvironmentVariable(key);
            
            // Also check configuration (which may have been expanded)
            var configKey = envToConfigMap.ContainsKey(key) ? envToConfigMap[key] : key;
            var configVal = configuration[configKey];
            
            // If both are empty, it's missing
            if (string.IsNullOrWhiteSpace(envVal) && string.IsNullOrWhiteSpace(configVal))
            {
                // For DB_PASSWORD, allow empty string (some MySQL configs don't require password)
                if (key == "DB_PASSWORD")
                {
                    logger.LogWarning("⚠️ DB_PASSWORD is empty. This is allowed but not recommended for production.");
                    continue;
                }
                
                missingVars.Add(key);
            }
        }

        // Validate JWT Secret Length
        var jwtSecret = configuration["JWT_SECRET_KEY"];
        if (!string.IsNullOrEmpty(jwtSecret) && Encoding.UTF8.GetByteCount(jwtSecret) < 32)
        {
            throw new InvalidOperationException("CRITICAL: JWT_SECRET_KEY is too short. It must be at least 32 bytes (characters) long for HMAC-SHA256 security.");
        }

        if (missingVars.Any())
        {
            var msg = "CRITICAL: Missing required configuration variables: " + string.Join(", ", missingVars);
            logger.LogCritical(msg);
            throw new InvalidOperationException(msg);
        }

        logger.LogInformation("✅ Configuration validation passed.");
    }
}
