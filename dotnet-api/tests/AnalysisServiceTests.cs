using Xunit;
using Moq;
using dotnet_api.Services;
using dotnet_api.Models.DTOs;
using Octokit;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using dotnet_api.Data;

namespace dotnet_api_tests
{
    public class AnalysisServiceTests
    {
        private readonly Mock<IGitHubClient> _githubClientMock;
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<IConfiguration> _configMock;
        private readonly Mock<ILogger<AnalysisService>> _loggerMock;
        private readonly Mock<ApplicationDbContext> _contextMock;

        public AnalysisServiceTests()
        {
            _githubClientMock = new Mock<IGitHubClient>();
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _configMock = new Mock<IConfiguration>();
            _loggerMock = new Mock<ILogger<AnalysisService>>();
            _contextMock = new Mock<ApplicationDbContext>();
        }

        [Theory]
        [InlineData("https://github.com/owner/repo", "owner", "repo")]
        [InlineData("https://github.com/Taha7486/TP1-php.git", "Taha7486", "TP1-php")]
        [InlineData("https://github.com/user-name/repo-name", "user-name", "repo-name")]
        public void ParseRepoUrl_ShouldCorrectlyParse(string url, string expectedOwner, string expectedName)
        {
            // Arrange
            var service = new AnalysisService(
                _configMock.Object,
                _httpClientFactoryMock.Object,
                _contextMock.Object,
                _loggerMock.Object,
                null,
                _githubClientMock.Object
            );

            // Act
            var result = service.ParseRepoUrl(url);

            // Assert
            Assert.Equal(expectedOwner, result.owner);
            Assert.Equal(expectedName, result.name);
        }

        [Fact]
        public void ParseRepoUrl_ShouldThrowOnInvalidUrl()
        {
            // Arrange
            var service = new AnalysisService(
                _configMock.Object,
                _httpClientFactoryMock.Object,
                _contextMock.Object,
                _loggerMock.Object,
                null,
                _githubClientMock.Object
            );
            string invalidUrl = "https://github.com/onlyonepath";

            // Act & Assert
            Assert.Throws<ArgumentException>(() => service.ParseRepoUrl(invalidUrl));
        }

        // Note: ExtractScore is a frontend function, not part of AnalysisService
        // Tests for parsing, filtering, and summary building are covered by ParseRepoUrl and ShouldAnalyzeFile tests

        [Theory]
        [InlineData("SGVsbG8gV29ybGQ=", true)] // "Hello World" in base64
        [InlineData("dGVzdA==", true)] // "test" in base64
        [InlineData("Hello World", false)]
        [InlineData("", false)]
        [InlineData("abc", false)] // Not valid base64 (length not multiple of 4)
        public void IsBase64String_ShouldCorrectlyIdentify(string input, bool expected)
        {
            // Arrange
            var service = new AnalysisService(
                _configMock.Object,
                _httpClientFactoryMock.Object,
                _contextMock.Object,
                _loggerMock.Object,
                null,
                _githubClientMock.Object
            );

            // Act
            var result = service.IsBase64String(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("test.php", ".php", true)]
        [InlineData("script.js", ".js", true)]
        [InlineData("file.txt", ".txt", false)]
        [InlineData("image.png", ".png", false)]
        public void ShouldAnalyzeFile_ShouldFilterByExtension(string filepath, string extension, bool shouldAnalyze)
        {
            // Arrange
            var configSection = new Mock<IConfigurationSection>();
            configSection.Setup(x => x.Get<string[]>()).Returns(new[] { ".php", ".js", ".jsx", ".ts", ".tsx", ".cs" });
            
            _configMock.Setup(x => x.GetSection("AnalysisLimits:AllowedExtensions")).Returns(configSection.Object);
            _configMock.Setup(x => x.GetValue<long>("AnalysisLimits:MaxFileSizeBytes", It.IsAny<long>())).Returns(1048576);
            
            var service = new AnalysisService(
                _configMock.Object,
                _httpClientFactoryMock.Object,
                _contextMock.Object,
                _loggerMock.Object,
                null,
                _githubClientMock.Object
            );

            // Act
            var result = service.ShouldAnalyzeFile(filepath, 1000, "test");

            // Assert
            Assert.Equal(shouldAnalyze, result);
        }
    }
}
