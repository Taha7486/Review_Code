using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using dotnet_api.Controllers;
using dotnet_api.Models.DTOs;
using dotnet_api.Services;
using System.Threading.Tasks;

namespace dotnet_api_tests
{
    public class AuthControllerTests
    {
        private readonly Mock<IAuthService> _authServiceMock;
        private readonly AuthController _controller;

        public AuthControllerTests()
        {
            _authServiceMock = new Mock<IAuthService>();
            _controller = new AuthController(_authServiceMock.Object);
        }

        [Fact]
        public async Task Register_ValidUser_ReturnsOkWithToken()
        {
            // Arrange
            var registerDto = new RegisterDto
            {
                Email = "test@example.com",
                Password = "Password123!",
                Username = "testuser"
            };

            var expectedResponse = new AuthResponseDto
            {
                Token = "fake-jwt-token",
                User = new UserDto
                {
                    Id = 1,
                    Email = "test@example.com",
                    Username = "testuser"
                }
            };

            _authServiceMock
                .Setup(s => s.RegisterAsync(registerDto))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.Register(registerDto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<AuthResponseDto>(okResult.Value);
            Assert.Equal("fake-jwt-token", response.Token);
            Assert.Equal("test@example.com", response.User.Email);
        }

        [Fact]
        public async Task Register_DuplicateEmail_ReturnsBadRequest()
        {
            // Arrange
            var registerDto = new RegisterDto
            {
                Email = "existing@example.com",
                Password = "Password123!",
                Username = "testuser"
            };

            _authServiceMock
                .Setup(s => s.RegisterAsync(registerDto))
                .ThrowsAsync(new System.Exception("Email already exists"));

            // Act & Assert
            await Assert.ThrowsAsync<System.Exception>(
                async () => await _controller.Register(registerDto)
            );
        }

        [Fact]
        public async Task Login_ValidCredentials_ReturnsToken()
        {
            // Arrange
            var loginDto = new LoginDto
            {
                Email = "test@example.com",
                Password = "Password123!"
            };

            var expectedResponse = new AuthResponseDto
            {
                Token = "valid-jwt-token",
                User = new UserDto
                {
                    Id = 1,
                    Email = "test@example.com",
                    Username = "testuser"
                }
            };

            _authServiceMock
                .Setup(s => s.LoginAsync(loginDto))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.Login(loginDto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<AuthResponseDto>(okResult.Value);
            Assert.Equal("valid-jwt-token", response.Token);
            Assert.NotNull(response.User);
        }

        [Fact]
        public async Task Login_InvalidPassword_ThrowsException()
        {
            // Arrange
            var loginDto = new LoginDto
            {
                Email = "test@example.com",
                Password = "WrongPassword"
            };

            _authServiceMock
                .Setup(s => s.LoginAsync(loginDto))
                .ThrowsAsync(new UnauthorizedAccessException("Invalid credentials"));

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                async () => await _controller.Login(loginDto)
            );
        }

        [Fact]
        public async Task Login_NonExistentUser_ThrowsException()
        {
            // Arrange
            var loginDto = new LoginDto
            {
                Email = "nonexistent@example.com",
                Password = "Password123!"
            };

            _authServiceMock
                .Setup(s => s.LoginAsync(loginDto))
                .ThrowsAsync(new System.Exception("User not found"));

            // Act & Assert
            await Assert.ThrowsAsync<System.Exception>(
                async () => await _controller.Login(loginDto)
            );
        }

        [Fact]
        public async Task Register_EmptyEmail_ThrowsException()
        {
            // Arrange
            var registerDto = new RegisterDto
            {
                Email = "",
                Password = "Password123!",
                Username = "testuser"
            };

            _authServiceMock
                .Setup(s => s.RegisterAsync(registerDto))
                .ThrowsAsync(new ArgumentException("Email is required"));

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                async () => await _controller.Register(registerDto)
            );
        }

        [Fact]
        public async Task Register_WeakPassword_ThrowsException()
        {
            // Arrange
            var registerDto = new RegisterDto
            {
                Email = "test@example.com",
                Password = "123", // Weak password
                Username = "testuser"
            };

            _authServiceMock
                .Setup(s => s.RegisterAsync(registerDto))
                .ThrowsAsync(new ArgumentException("Password must be at least 6 characters"));

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                async () => await _controller.Register(registerDto)
            );
        }
    }
}
