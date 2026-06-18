using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using SapApi.Domain.Interfaces;
using SapApi.Domain.Models;
using SapApi.Infrastructure.Services;
using SapApi.Shared.Configuration;

namespace SapApi.Tests.Sap;

[TestFixture]
public class SapLoginServiceTests
{
    private Mock<ISapSessionStore> _sessionStore = null!;
    private Mock<IAesEncryptionService> _aes = null!;
    private IHttpContextAccessor _httpContextAccessor = null!;
    private SapLoginService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _sessionStore = new Mock<ISapSessionStore>();
        _aes = new Mock<IAesEncryptionService>();
        _httpContextAccessor = new HttpContextAccessor();
        _sut = new SapLoginService(
            _sessionStore.Object,
            _httpContextAccessor,
            _aes.Object,
            Options.Create(new SapCredentials { CompanyDb = "TEST_DB" }));
    }

    [Test]
    public async Task GetSessionIdAsync_NoAuthenticatedUser_ReturnsNull()
    {
        var sessionId = await _sut.GetSessionIdAsync();

        sessionId.Should().BeNull();
        _sessionStore.Verify(s => s.GetSessionAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GetSessionIdAsync_ValidSession_ReturnsSessionId()
    {
        SetUser(15);
        _sessionStore
            .Setup(s => s.GetSessionAsync(15, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapSessionInfo
            {
                SessionId = "valid-session",
                UserName = "sap-user",
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10),
            });

        var sessionId = await _sut.GetSessionIdAsync();

        sessionId.Should().Be("valid-session");
    }

    [Test]
    public async Task GetSessionIdAsync_ExpiredSession_RemovesAndReturnsNull()
    {
        SetUser(22);
        _sessionStore
            .Setup(s => s.GetSessionAsync(22, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapSessionInfo
            {
                SessionId = "expired-session",
                UserName = "sap-user",
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1),
            });

        var sessionId = await _sut.GetSessionIdAsync();

        sessionId.Should().BeNull();
        _sessionStore.Verify(s => s.RemoveSessionAsync(22, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task SapLoginAsync_ValidSession_DoesNotRenew()
    {
        SetUser(8);
        _sessionStore
            .Setup(s => s.GetSessionAsync(8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapSessionInfo
            {
                SessionId = "active",
                UserName = "sap-user",
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(20),
            });

        await _sut.SapLoginAsync();

        _sessionStore.Verify(s => s.GetCredentialsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task LogoutAsync_RemovesStoredSession()
    {
        SetUser(3);
        _sessionStore
            .Setup(s => s.GetSessionAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapSessionInfo
            {
                SessionId = "logout-me",
                UserName = "sap-user",
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
            });

        await _sut.LogoutAsync();

        _sessionStore.Verify(s => s.RemoveSessionAsync(3, It.IsAny<CancellationToken>()), Times.Once);
    }

    private void SetUser(int userId)
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "Test");
        _httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
        };
    }
}
