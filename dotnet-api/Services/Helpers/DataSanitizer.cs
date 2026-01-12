using System.IO.Compression;
using System.Text;

namespace dotnet_api.Services.Helpers;

/// <summary>
/// Handles data compression, decompression, and sanitization
/// </summary>
public interface IDataSanitizer
{
    string CompressString(string text);
    string? DecompressString(string? compressedText);
    string RedactSensitiveData(string input);
}

public class DataSanitizer : IDataSanitizer
{
    public string CompressString(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var bytes = Encoding.UTF8.GetBytes(text);
        using var outputStream = new MemoryStream();
        using (var gzipStream = new GZipStream(outputStream, CompressionMode.Compress))
        {
            gzipStream.Write(bytes, 0, bytes.Length);
        }
        return Convert.ToBase64String(outputStream.ToArray());
    }

    public string? DecompressString(string? compressedText)
    {
        if (string.IsNullOrWhiteSpace(compressedText)) return null;

        try
        {
            var compressedBytes = Convert.FromBase64String(compressedText);
            using var inputStream = new MemoryStream(compressedBytes);
            using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);
            using var outputStream = new MemoryStream();
            gzipStream.CopyTo(outputStream);
            return Encoding.UTF8.GetString(outputStream.ToArray());
        }
        catch
        {
            // If decompression fails, assume it's uncompressed text
            return compressedText;
        }
    }

    public string RedactSensitiveData(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;

        // Redact github tokens (ghp_xxx, github_pat_xxx)
        input = System.Text.RegularExpressions.Regex.Replace(input, @"ghp_[A-Za-z0-9]{36}", "[REDACTED_TOKEN]");
        input = System.Text.RegularExpressions.Regex.Replace(input, @"github_pat_[A-Za-z0-9_]+", "[REDACTED_TOKEN]");

        return input;
    }
}
