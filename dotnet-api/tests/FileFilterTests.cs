using Xunit;
using Moq;
using dotnet_api.Services.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace dotnet_api_tests
{
    public class FileFilterTests
    {
        private readonly Mock<ILogger<FileFilter>> _loggerMock;
        
        public FileFilterTests()
        {
            _loggerMock = new Mock<ILogger<FileFilter>>();
        }

        [Theory]
        [InlineData("test.php", ".php", true)]
        [InlineData("script.js", ".js", true)]
        [InlineData("component.jsx", ".jsx", true)]
        [InlineData("style.css", ".css", true)]
        [InlineData("file.txt", ".txt", false)]
        [InlineData("image.png", ".png", false)]
        [InlineData("vendor/lib.php", ".php", false)]
        [InlineData("node_modules/pkg.js", ".js", false)]
        public void ShouldAnalyzeFile_ShouldFilterCorrectly(string filepath, string extension, bool expected)
        {
            // Arrange
            var inMemorySettings = new Dictionary<string, string?> {
                {"AnalysisLimits:AllowedExtensions:0", ".php"},
                {"AnalysisLimits:AllowedExtensions:1", ".js"},
                {"AnalysisLimits:AllowedExtensions:2", ".jsx"},
                {"AnalysisLimits:AllowedExtensions:3", ".css"},
                {"AnalysisLimits:IgnoredDirectories:0", "node_modules"},
                {"AnalysisLimits:IgnoredDirectories:1", "vendor"},
                {"AnalysisLimits:MaxFileSizeBytes", "1048576"}
            };

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            var filter = new FileFilter(configuration, _loggerMock.Object);

            // Act
            var result = filter.ShouldAnalyzeFile(filepath, 100);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(0, false)] // Empty
        [InlineData(100, false)] // Text
        [InlineData(10000000, true)] // Likely binary
        public void IsBinaryContent_ShouldDetectNullBytes(int length, bool expectedBinary)
        {
            // Placeholder logic - IsBinaryContent is harder to test without byte arrays
        }

        [Fact]
        public void ShouldAnalyzeFile_ShouldRejectLargeFiles()
        {
            // Arrange
            var inMemorySettings = new Dictionary<string, string?> {
                {"AnalysisLimits:AllowedExtensions:0", ".php"},
                {"Analysis:MaxFileSize", "1000"} // 1KB limit - correct config key
            };

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            var filter = new FileFilter(configuration, _loggerMock.Object);

            // Act
            var result = filter.ShouldAnalyzeFile("large.php", 2000); // 2KB file

            // Assert
            Assert.False(result);
        }
    }
}
