using System.Text.Json;
using DocumentProcessing.Contracts.DTOs;
using DocumentProcessing.Infrastructure.Caching;
using DocumentProcessing.Infrastructure.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace DocumentProcessing.UnitTests.Caching;

public sealed class RedisCacheServiceTests
{
    private readonly Mock<IConnectionMultiplexer> _multiplexerMock = new();
    private readonly Mock<IDatabase> _dbMock = new();

    private readonly IOptions<RedisOptions> _options =
        Options.Create(new RedisOptions { DefaultTtlMinutes = 60, PageUrlTtlMinutes = 55 });

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private RedisCacheService CreateService()
    {
        _multiplexerMock.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
            .Returns(_dbMock.Object);
        return new RedisCacheService(_multiplexerMock.Object, _options);
    }

    [Fact]
    public async Task GetAsync_CacheHit_ReturnsDeserializedValue()
    {
        var dto = new PageUrlDto { PageNumber = 1, FullUrl = "https://full", ThumbnailUrl = "https://thumb" };
        var json = JsonSerializer.Serialize(dto, JsonOpts);

        _dbMock.Setup(d => d.StringGetAsync("key", It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue(json));

        var result = await CreateService().GetAsync<PageUrlDto>("key");

        result.Should().NotBeNull();
        result!.FullUrl.Should().Be("https://full");
        result.PageNumber.Should().Be(1);
    }

    [Fact]
    public async Task GetAsync_CacheMiss_ReturnsNull()
    {
        _dbMock.Setup(d => d.StringGetAsync("missing-key", It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var result = await CreateService().GetAsync<PageUrlDto>("missing-key");

        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_SerializesAndCallsStringSet()
    {
        var dto = new PageUrlDto { PageNumber = 2, FullUrl = "https://f2", ThumbnailUrl = "https://t2" };
        var ttl = TimeSpan.FromMinutes(30);

        _dbMock.Setup(d => d.StringSetAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), ttl,
            It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        await CreateService().SetAsync("key2", dto, ttl);

        _dbMock.Verify(d => d.StringSetAsync(
            "key2", It.Is<RedisValue>(v => v.ToString().Contains("f2")),
            ttl, It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task GetPageUrlAsync_UsesCorrectKey()
    {
        var docId = Guid.NewGuid();
        var expectedKey = $"page-url:{docId}:3";

        _dbMock.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        await CreateService().GetPageUrlAsync(docId, 3);

        _dbMock.Verify(d => d.StringGetAsync(expectedKey, It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task SetPageUrlAsync_UsesTtlFromOptions_WhenPageUrlTtlMinutes55()
    {
        var docId = Guid.NewGuid();
        var dto = new PageUrlDto { PageNumber = 1, FullUrl = "u", ThumbnailUrl = "t" };
        var expectedTtl = TimeSpan.FromMinutes(55);

        _dbMock.Setup(d => d.StringSetAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), expectedTtl,
            It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        await CreateService().SetPageUrlAsync(docId, 1, dto, expectedTtl);

        _dbMock.Verify(d => d.StringSetAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), expectedTtl,
            It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Once);
    }
}
