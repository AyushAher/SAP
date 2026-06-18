using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SapApi.Domain.Models;
using SapApi.Infrastructure.Caching;

namespace SapApi.Tests.Caching;

[TestFixture]
public class DistributedCacheSapSessionStoreTests
{
    private IDistributedCache _cache = null!;
    private DistributedCacheSapSessionStore _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        _sut = new DistributedCacheSapSessionStore(_cache);
    }

    [Test]
    public async Task SetSessionAsync_ThenGetSessionAsync_RoundTripsCompressedPayload()
    {
        const int userId = 42;
        var session = new SapSessionInfo
        {
            SessionId = "B1SESSION-abc",
            UserName = "sap-user",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30),
        };
        var credentials = new SapRenewalCredentials
        {
            UserName = "sap-user",
            EncryptedPassword = "encrypted-secret",
        };

        await _sut.SetSessionAsync(userId, session, credentials, TimeSpan.FromMinutes(30));

        var loadedSession = await _sut.GetSessionAsync(userId);
        var loadedCredentials = await _sut.GetCredentialsAsync(userId);

        loadedSession.Should().BeEquivalentTo(session);
        loadedCredentials.Should().BeEquivalentTo(credentials);
    }

    [Test]
    public async Task RemoveSessionAsync_ClearsSessionAndCredentials()
    {
        const int userId = 7;
        await _sut.SetSessionAsync(
            userId,
            new SapSessionInfo { SessionId = "sid", UserName = "u", ExpiresAtUtc = DateTime.UtcNow.AddHours(1) },
            new SapRenewalCredentials { UserName = "u", EncryptedPassword = "p" },
            TimeSpan.FromHours(1));

        await _sut.RemoveSessionAsync(userId);

        (await _sut.GetSessionAsync(userId)).Should().BeNull();
        (await _sut.GetCredentialsAsync(userId)).Should().BeNull();
    }

    [Test]
    public async Task GetSessionAsync_UnknownUser_ReturnsNull()
    {
        (await _sut.GetSessionAsync(999)).Should().BeNull();
    }
}
