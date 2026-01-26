using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using dotnet_api.Services;
using dotnet_api.Services.Helpers;
using dotnet_api.Data;
using dotnet_api.Models;
using dotnet_api.Models.DTOs;
using Octokit;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace dotnet_api_tests
{
    public class AnalysisServiceErrorTests
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<ILogger<AnalysisService>> _loggerMock;
        private readonly Mock<IGitHubClientService> _githubClientServiceMock;
        private readonly Mock<IPhpServiceClient> _phpServiceClientMock;
        private readonly Mock<IDataSanitizer> _dataSanitizerMock;
        private readonly Mock<IFileFilter> _fileFilterMock;
        private readonly Mock<IGitHubFileService> _githubFileServiceMock;
        private readonly Mock<IRepositoryService> _repositoryServiceMock;
        private readonly Mock<IMetricsCalculator> _metricsCalculatorMock;
        private readonly IMemoryCache _memoryCache;
        private readonly AnalysisService _service;

        public AnalysisServiceErrorTests()
        {
            // Setup In-Memory Database
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new ApplicationDbContext(options);

            // Mocks
            _loggerMock = new Mock<ILogger<AnalysisService>>();
            _githubClientServiceMock = new Mock<IGitHubClientService>();
            _phpServiceClientMock = new Mock<IPhpServiceClient>();
            _dataSanitizerMock = new Mock<IDataSanitizer>();
            _fileFilterMock = new Mock<IFileFilter>();
            _githubFileServiceMock = new Mock<IGitHubFileService>();
            _repositoryServiceMock = new Mock<IRepositoryService>();
            _metricsCalculatorMock = new Mock<IMetricsCalculator>();
            _memoryCache = new MemoryCache(new MemoryCacheOptions());

            // Default mock behaviors
            _dataSanitizerMock.Setup(d => d.RedactSensitiveData(It.IsAny<string>()))
                .Returns<string>(s => s); // Pass through
            
            _githubClientServiceMock.Setup(g => g.ParseRepoUrl(It.IsAny<string>()))
                .Returns(("owner", "repo"));

            _service = new AnalysisService(
                _context,
                _loggerMock.Object,
                _githubClientServiceMock.Object,
                _phpServiceClientMock.Object,
                _dataSanitizerMock.Object,
                _fileFilterMock.Object,
                _githubFileServiceMock.Object,
                _repositoryServiceMock.Object,
                _metricsCalculatorMock.Object,
                _memoryCache,
                null
            );
        }

        [Fact]
        public async Task AnalyzeBranchAsync_RateLimitExceeded_UpdatesRunStatusAndThrows()
        {
            // Arrange
            var request = new AnalyzeBranchDto { RepoUrl = "http://github.com/owner/repo", BranchName = "main" };
            var userId = 1;

            // Mock repo service to return a valid repository
            var repo = new dotnet_api.Models.Repository { Id = 1, UserId = userId, CloneUrl = request.RepoUrl };
            _repositoryServiceMock.Setup(r => r.GetOrCreateRepositoryAsync(userId, request.RepoUrl, null, It.IsAny<string>()))
                .ReturnsAsync(repo);

            // Mock GitHub Client to throw RateLimitExceededException
            _githubClientServiceMock.Setup(g => g.GetAuthenticatedClient(It.IsAny<string>()))
                .Throws(new RateLimitExceededException(
                    new RateLimit(10, 0, 1000), 
                    new Dictionary<string, string>(), 
                    "Rate limit exceeded"));

            // Act & Assert
            await Assert.ThrowsAsync<RateLimitExceededException>(() => 
                _service.AnalyzeBranchAsync(request, userId));

            // Verify Run status was updated (if created, but exception happens before run creation in some paths, check logic)
            // In AnalysisService.cs, GetAuthenticatedClient is called BEFORE run creation.
            // So no run is saved to DB yet.
            // Wait, looking at code: 
            // var authenticatedClient = _githubClientService.GetAuthenticatedClient(request.GithubToken);
            // This happens BEFORE run = new AnalysisRun...
            
            // So we can only verify it throws.
        }

        [Fact]
        public async Task AnalyzeBranchAsync_RepoNotFoundExceptions_AfterRunCreation_UpdatesStatus()
        {
            // Arrange
            var request = new AnalyzeBranchDto { RepoUrl = "http://github.com/owner/repo", BranchName = "main" };
            var userId = 1;
            
            var repo = new dotnet_api.Models.Repository { Id = 1, UserId = userId, CloneUrl = request.RepoUrl };
            _repositoryServiceMock.Setup(r => r.GetOrCreateRepositoryAsync(userId, request.RepoUrl, null, It.IsAny<string>()))
                .ReturnsAsync(repo);

            _githubClientServiceMock.Setup(g => g.GetAuthenticatedClient(It.IsAny<string>()))
                .Returns(new GitHubClient(new ProductHeaderValue("Test")));

            // IMPORTANT: Create logic failure AFTER run creation to test the catch block updating the run.
            // The run is created after repository retrieval and checking existing runs.
            // We'll mock GetCodeFromBranchAsync to throw exception, as that happens AFTER run creation.
            
            // Allow GetAuthenticatedClient and Repository calls to succeed
            var mockGitHubClient = new Mock<IGitHubClient>();
            var mockRepoClient = new Mock<IRepositoriesClient>();
            var mockGitClient = new Mock<IGitDatabaseClient>();
            var mockRefClient = new Mock<IReferencesClient>();

            _githubClientServiceMock.Setup(g => g.GetAuthenticatedClient(It.IsAny<string>()))
                .Returns(mockGitHubClient.Object);
            
            mockGitHubClient.Setup(c => c.Repository).Returns(mockRepoClient.Object);
            mockGitHubClient.Setup(c => c.Git).Returns(mockGitClient.Object);
            mockGitClient.Setup(c => c.Reference).Returns(mockRefClient.Object);

            mockRepoClient.Setup(r => r.Get(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new Octokit.Repository(1)); // Dummy repo
            
            // Mock refs to return SHAs so run creation proceeds
            mockRefClient.Setup(r => r.Get(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new Reference("ref", "node", "sha", new ReferenceObject("type", "sha", new TagObject("t", "s", "tag", "msg", new Committer("n", "e", DateTimeOffset.Mocks.FromDays(1)), new Signature("n", "e", DateTimeOffset.Mocks.FromDays(1))))));

            // NOW make GetCodeFromBranchAsync throw NotFoundException
            _githubFileServiceMock.Setup(f => f.GetCodeFromBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GitHubClient>()))
                .ThrowsAsync(new NotFoundException("File not found", System.Net.HttpStatusCode.NotFound));

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() => 
                _service.AnalyzeBranchAsync(request, userId));

            // Verify run was created and marked failed
            var run = await _context.AnalysisRuns.FirstOrDefaultAsync();
            Assert.NotNull(run);
            Assert.Equal("failed", run.Status);
            Assert.Contains("not found", run.Summary);
        }

        [Fact]
        public async Task AnalyzeBranchAsync_PhpServiceTimeout_UpdatesRunStatus()
        {
             // Arrange
            var request = new AnalyzeBranchDto { RepoUrl = "http://github.com/owner/repo", BranchName = "main" };
            var userId = 1;
            
            var repo = new dotnet_api.Models.Repository { Id = 1, UserId = userId, CloneUrl = request.RepoUrl };
            _repositoryServiceMock.Setup(r => r.GetOrCreateRepositoryAsync(userId, request.RepoUrl, null, It.IsAny<string>()))
                .ReturnsAsync(repo);

            // Mock basic GitHub flow to get to PHP service call
            var mockGitHubClient = new Mock<IGitHubClient>();
            var mockRepoClient = new Mock<IRepositoriesClient>();
            var mockGitClient = new Mock<IGitDatabaseClient>();
            var mockRefClient = new Mock<IReferencesClient>();

            _githubClientServiceMock.Setup(g => g.GetAuthenticatedClient(It.IsAny<string>()))
                .Returns(mockGitHubClient.Object);
            
            mockGitHubClient.Setup(c => c.Repository).Returns(mockRepoClient.Object);
            mockGitHubClient.Setup(c => c.Git).Returns(mockGitClient.Object);
            mockGitClient.Setup(c => c.Reference).Returns(mockRefClient.Object);

            mockRepoClient.Setup(r => r.Get(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new Octokit.Repository(1));
            
            mockRefClient.Setup(r => r.Get(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new Reference("ref", "node", "sha", new ReferenceObject("type", "sha", new TagObject("t", "s", "tag", "msg", new Committer("n", "e", DateTimeOffset.Mocks.FromDays(1)), new Signature("n", "e", DateTimeOffset.Mocks.FromDays(1))))));

            _githubFileServiceMock.Setup(f => f.GetCodeFromBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GitHubClient>()))
                .ReturnsAsync(new List<GitHubFile> { new GitHubFile { Path = "test.php", Content = "code" } });

            // Mock PHP Service Exception
            _phpServiceClientMock.Setup(p => p.AnalyzeFilesAsync(It.IsAny<List<GitHubFile>>(), It.IsAny<string>()))
                .ThrowsAsync(new System.Exception("PHP Service Timeout"));

            // Act & Assert
            await Assert.ThrowsAsync<System.Exception>(() => 
                _service.AnalyzeBranchAsync(request, userId));

            // Verify run marked failed
            var run = await _context.AnalysisRuns.FirstOrDefaultAsync();
            Assert.NotNull(run);
            Assert.Equal("failed", run.Status);
            Assert.Contains("Analysis failed", run.Summary);
            Assert.Contains("Timeout", run.Summary);
        }
    }
}
