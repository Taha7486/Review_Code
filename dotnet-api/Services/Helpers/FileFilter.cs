namespace dotnet_api.Services.Helpers;

/// <summary>
/// Handles file filtering and validation logic
/// </summary>
public interface IFileFilter
{
    bool ShouldAnalyzeFile(string filename, long? fileSize);
    bool IsBase64String(string content);
    bool IsBinaryContent(string content);
    List<T> ApplyFileCountLimit<T>(List<T> files, string correlationId, ILogger logger) where T : class;
}

public class FileFilter : IFileFilter
{
    private readonly IConfiguration _config;
    private readonly ILogger<FileFilter> _logger;

    // Analyzed extensions
    private static readonly HashSet<string> AnalyzedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".php", ".js", ".jsx", ".ts", ".tsx", ".py", ".java", ".c", ".cpp", ".cs",
        ".rb", ".go", ".rs", ".swift", ".kt", ".scala", ".html", ".css", ".vue"
    };

    // Ignored paths
    private static readonly HashSet<string> IgnoredPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", "vendor", ".git", "dist", "build", "coverage", ".next", "out"
    };

    public FileFilter(IConfiguration config, ILogger<FileFilter> logger)
    {
        _config = config;
        _logger = logger;
    }

    public bool ShouldAnalyzeFile(string filename, long? fileSize)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return false;

        // Skip large files (default: 500KB)
        var maxFileSize = _config.GetValue("Analysis:MaxFileSize", 512000);
        if (fileSize.HasValue && fileSize > maxFileSize)
        {
            _logger.LogDebug("Skipping {FileName}: file too large ({FileSize} bytes)", filename, fileSize);
            return false;
        }

        // Skip ignored directories
        var pathParts = filename.Split('/');
        if (pathParts.Any(part => IgnoredPaths.Contains(part)))
        {
            _logger.LogDebug("Skipping {FileName}: in ignored directory", filename);
            return false;
        }

        // Only analyze supported file types
        var ext = Path.GetExtension(filename);
        if (!AnalyzedExtensions.Contains(ext))
        {
            _logger.LogDebug("Skipping {FileName}: unsupported extension {Extension}", filename, ext);
            return false;
        }

        return true;
    }

    public bool IsBase64String(string content)
    {
        if (string.IsNullOrWhiteSpace(content) || content.Length < 50)
            return false;

        try
        {
            var base64Chars = content.Take(100).Count(c =>
                char.IsLetterOrDigit(c) || c == '+' || c == '/' || c == '=');
            return (double)base64Chars / Math.Min(100, content.Length) > 0.9;
        }
        catch
        {
            return false;
        }
    }

    public bool IsBinaryContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        var sample = content.Take(1024).ToArray();
        var nonPrintable = sample.Count(c => char.IsControl(c) && c != '\n' && c != '\r' && c != '\t');
        return (double)nonPrintable / sample.Length > 0.3;
    }

    public List<T> ApplyFileCountLimit<T>(List<T> files, string correlationId, ILogger logger) where T : class
    {
        var maxFiles = _config.GetValue("Analysis:MaxFilesPerRun", 50);

        if (files.Count > maxFiles)
        {
            logger.LogWarning(
                "[{CorrelationId}] File count ({FileCount}) exceeds limit ({MaxFiles}). Truncating to first {MaxFiles} files.",
                correlationId, files.Count, maxFiles, maxFiles);

            return files.Take(maxFiles).ToList();
        }

        return files;
    }
}
