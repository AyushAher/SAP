using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SapApi.Infrastructure.Caching;

namespace SapApi.Tests.Caching;

[TestFixture]
public class SapMasterDataCacheTests
{
    private IDistributedCache _cache = null!;
    private SapMasterDataCache _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        _sut = new SapMasterDataCache(_cache);
    }

    [Test]
    public async Task GetOrCreateAsync_CacheMiss_InvokesFactoryAndCachesResult()
    {
        var callCount = 0;
        Task<string?> Factory()
        {
            callCount++;
            return Task.FromResult<string?>("live-value");
        }

        var first = await _sut.GetOrCreateAsync("key-1", Factory, TimeSpan.FromMinutes(5));
        var second = await _sut.GetOrCreateAsync("key-1", Factory, TimeSpan.FromMinutes(5));

        first.Should().Be("live-value");
        second.Should().Be("live-value");
        callCount.Should().Be(1, "the second call should be served from cache, not hit the factory again");
    }

    [Test]
    public async Task GetOrCreateAsync_DifferentKeys_AreCachedIndependently()
    {
        var result1 = await _sut.GetOrCreateAsync("items::select=ItemCode", () => Task.FromResult<string?>("A"), TimeSpan.FromMinutes(5));
        var result2 = await _sut.GetOrCreateAsync("items::select=ItemCode,ItemName", () => Task.FromResult<string?>("B"), TimeSpan.FromMinutes(5));

        result1.Should().Be("A");
        result2.Should().Be("B");
    }

    [Test]
    public async Task GetOrCreateAsync_FactoryReturnsNull_DoesNotCacheNegativeResult()
    {
        var callCount = 0;
        Task<string?> Factory()
        {
            callCount++;
            return Task.FromResult<string?>(null);
        }

        await _sut.GetOrCreateAsync("key-null", Factory, TimeSpan.FromMinutes(5));
        await _sut.GetOrCreateAsync("key-null", Factory, TimeSpan.FromMinutes(5));

        callCount.Should().Be(2, "a transient failure (null) must not be cached as a false negative for the TTL duration");
    }

    [Test]
    public async Task GetOrCreateAsync_AfterExpiry_RefetchesFromFactory()
    {
        var callCount = 0;
        Task<string?> Factory()
        {
            callCount++;
            return Task.FromResult<string?>($"value-{callCount}");
        }

        var first = await _sut.GetOrCreateAsync("key-ttl", Factory, TimeSpan.FromMilliseconds(50));
        await Task.Delay(150);
        var second = await _sut.GetOrCreateAsync("key-ttl", Factory, TimeSpan.FromMilliseconds(50));

        first.Should().Be("value-1");
        second.Should().Be("value-2");
        callCount.Should().Be(2);
    }
}
