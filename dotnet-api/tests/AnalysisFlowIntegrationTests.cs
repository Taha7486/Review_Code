using Xunit;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace dotnet_api_tests.Integration
{
    /// <summary>
    /// Integration tests for the full analysis workflow
    /// These tests verify the end-to-end flow from repository submission to analysis completion
    /// </summary>
    public class AnalysisFlowIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public AnalysisFlowIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    // Override configuration for testing
                    var testConfig = new Dictionary<string, string?>
                    {
                        {"DB_HOST", "localhost"},
                        {"DB_NAME", "code_review_tool_test"},
                        {"DB_USER", "root"},
                        {"DB_PASSWORD", ""},
                        {"JWT_SECRET_KEY", "test_secret_key_for_integration_tests_at_least_32_characters_long"},
                        {"PHP_ANALYSIS_API_URL", "http://localhost:8000/api/analyze/files"},
                        {"INTERNAL_SERVICE_SECRET", "test_secret"},
                        {"ALLOWED_ORIGINS", "http://localhost:3000"}
                    };
                    config.AddInMemoryCollection(testConfig);
                });
            });

            _client = _factory.CreateClient();
        }

        /// <summary>
        /// Test 1: Verify that health check endpoint responds correctly
        /// This ensures the API is up and running before attempting analysis
        /// </summary>
        [Fact]
        public async Task HealthCheck_ShouldReturnHealthy()
        {
            // Act
            var response = await _client.GetAsync("/health");

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            Assert.NotNull(content);
        }

        /// <summary>
        /// Test 2: Complete authentication flow
        /// Verifies user registration, login, and JWT token generation
        /// NOTE: This test requires a properly configured test database
        /// </summary>
        [Fact(Skip = "Requires test database setup. Run manually with: dotnet ef database update --connection 'Server=localhost;Database=code_review_tool_test;User=root;Password=;'")]
        public async Task AuthFlow_RegisterAndLogin_ShouldReturnToken()
        {
            // Arrange
            var registerPayload = new
            {
                username = $"testuser_{DateTime.UtcNow.Ticks}",
                email = $"test_{DateTime.UtcNow.Ticks}@example.com",
                password = "TestPassword123!"
            };

            var registerContent = new StringContent(
                JsonSerializer.Serialize(registerPayload),
                Encoding.UTF8,
                "application/json"
            );

            // Act - Register
            var registerResponse = await _client.PostAsync("/api/auth/register", registerContent);

            // Assert - Registration successful (or get detailed error)
            if (!registerResponse.IsSuccessStatusCode && registerResponse.StatusCode != System.Net.HttpStatusCode.Conflict)
            {
                var errorContent = await registerResponse.Content.ReadAsStringAsync();
                Assert.Fail($"Registration failed with status {registerResponse.StatusCode}. Response: {errorContent}");
            }

            // Act - Login
            var loginPayload = new
            {
                email = registerPayload.email,
                password = registerPayload.password
            };

            var loginContent = new StringContent(
                JsonSerializer.Serialize(loginPayload),
                Encoding.UTF8,
                "application/json"
            );

            var loginResponse = await _client.PostAsync("/api/auth/login", loginContent);

            // Assert - Login successful and returns token
            Assert.True(loginResponse.IsSuccessStatusCode, 
                $"Login failed with status {loginResponse.StatusCode}. Response: {await loginResponse.Content.ReadAsStringAsync()}");

            var loginResult = await loginResponse.Content.ReadAsStringAsync();
            var loginData = JsonSerializer.Deserialize<JsonElement>(loginResult);

            Assert.True(loginData.TryGetProperty("token", out var token));
            Assert.False(string.IsNullOrEmpty(token.GetString()));
        }

        /// <summary>
        /// Test 3: Full analysis flow with public repository
        /// This is the most important integration test - verifies the complete workflow:
        /// 1. Authenticate user
        /// 2. Submit repository for analysis
        /// 3. Verify analysis starts (returns runId)
        /// 4. Poll for completion (with timeout)
        /// </summary>
        [Fact(Skip = "Requires PHP service and database to be running. Enable for full integration testing.")]
        public async Task AnalysisFlow_PublicRepository_ShouldCompleteSuccessfully()
        {
            // Arrange - Register and login
            var timestamp = DateTime.UtcNow.Ticks;
            var registerPayload = new
            {
                username = $"integrationtest_{timestamp}",
                email = $"integration_{timestamp}@example.com",
                password = "IntegrationTest123!"
            };

            var registerContent = new StringContent(
                JsonSerializer.Serialize(registerPayload),
                Encoding.UTF8,
                "application/json"
            );

            await _client.PostAsync("/api/auth/register", registerContent);

            var loginContent = new StringContent(
                JsonSerializer.Serialize(new { email = registerPayload.email, password = registerPayload.password }),
                Encoding.UTF8,
                "application/json"
            );

            var loginResponse = await _client.PostAsync("/api/auth/login", loginContent);
            var loginResult = await loginResponse.Content.ReadAsStringAsync();
            var loginData = JsonSerializer.Deserialize<JsonElement>(loginResult);
            var token = loginData.GetProperty("token").GetString();

            // Add authorization header
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act - Start analysis on a small public PHP repository
            var analysisPayload = new
            {
                repoUrl = "https://github.com/slimphp/Slim",
                branchName = "4.x",
                githubToken = (string?)null // Public repo, no token needed
            };

            var analysisContent = new StringContent(
                JsonSerializer.Serialize(analysisPayload),
                Encoding.UTF8,
                "application/json"
            );

            var startResponse = await _client.PostAsync("/api/analysis/start", analysisContent);

            // Assert - Analysis started
            Assert.True(startResponse.IsSuccessStatusCode, 
                $"Analysis start failed: {await startResponse.Content.ReadAsStringAsync()}");

            var startResult = await startResponse.Content.ReadAsStringAsync();
            var startData = JsonSerializer.Deserialize<JsonElement>(startResult);
            
            Assert.True(startData.TryGetProperty("runId", out var runIdElement));
            var runId = runIdElement.GetInt32();
            Assert.True(runId > 0);

            // Act - Poll for completion (max 60 seconds)
            var maxAttempts = 12; // 12 attempts * 5 seconds = 60 seconds
            var completed = false;
            string? status = null;

            for (int i = 0; i < maxAttempts; i++)
            {
                await Task.Delay(5000); // Wait 5 seconds between polls

                var statusResponse = await _client.GetAsync($"/api/analysis/runs/{runId}");
                
                if (statusResponse.IsSuccessStatusCode)
                {
                    var statusResult = await statusResponse.Content.ReadAsStringAsync();
                    var statusData = JsonSerializer.Deserialize<JsonElement>(statusResult);
                    status = statusData.GetProperty("status").GetString();

                    if (status == "completed" || status == "failed")
                    {
                        completed = true;
                        break;
                    }
                }
            }

            // Assert - Analysis completed or failed (not stuck in processing)
            Assert.True(completed, $"Analysis did not complete within timeout. Last status: {status}");
            Assert.Equal("completed", status); // Ideally should complete successfully
        }
    }
}
