using DocumentProcessing.Contracts.DTOs;
using DocumentProcessing.Core.Domain;
using DocumentProcessing.Core.Interfaces;
using DocumentProcessing.Core.Queries;
using DocumentProcessing.Core.ValueObjects;
using DocumentProcessing.Infrastructure.CQRS.Handlers;
using DocumentProcessing.Infrastructure.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace DocumentProcessing.UnitTests.Handlers;

public sealed class GetPageUrlQueryHandlerTests
{
    private readonly Mock<IDocumentRepository> _repositoryMock = new();
    private readonly Mock<IStorageService> _storageMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();

    private readonly IOptions<RedisOptions> _redisOptions =
        Options.Create(new RedisOptions { PageUrlTtlMinutes = 55 });

    private GetPageUrlQueryHandler CreateHandler() =>
        new(_repositoryMock.Object, _storageMock.Object, _cacheMock.Object, _redisOptions);

    private static Document BuildReadyDocument(Guid docId, Guid tenantDoc)
    {
        var doc = Document.CreateUploadIntent(
            new DocumentId(docId),
            TenantId.From("t1"),
            "f.pdf",
            "t1/blob",
            Checksum.From(new string('a', 64)),
            100,
            "c");
        doc.MarkUploaded("c");
        doc.MarkProcessing("c");

        var page = DocumentPage.Create(docId, PageNumber.From(1),
            "pages/doc/page-0001-full.webp", "pages/doc/page-0001-thumb.webp", 800, 1200);
        doc.AddPages([page]);
        doc.MarkReady(1, "c");
        return doc;
    }

    [Fact]
    public async Task HandleAsync_CacheHit_ReturnsCachedDto_WithoutStorageCall()
    {
        var docId = Guid.NewGuid();
        var query = new GetPageUrlQuery { DocumentId = docId, PageNumber = 1, TenantId = "t1" };

        var cached = new PageUrlDto { PageNumber = 1, FullUrl = "https://cdn/full", ThumbnailUrl = "https://cdn/thumb" };

        _cacheMock.Setup(c => c.GetPageUrlAsync(docId, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cached);

        var result = await CreateHandler().HandleAsync(query);

        result.Should().BeSameAs(cached);
        _storageMock.Verify(s => s.GetPageCdnUrlsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _repositoryMock.Verify(r => r.GetByIdAndTenantAsync(It.IsAny<DocumentId>(), It.IsAny<TenantId>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_CacheMiss_FetchesFromDb_GeneratesCdnUrl_AndCaches()
    {
        var docId = Guid.NewGuid();
        var query = new GetPageUrlQuery { DocumentId = docId, PageNumber = 1, TenantId = "t1" };
        var doc = BuildReadyDocument(docId, docId);
        var expiry = DateTimeOffset.UtcNow.AddHours(1);

        _cacheMock.Setup(c => c.GetPageUrlAsync(docId, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PageUrlDto?)null);

        _repositoryMock.Setup(r => r.GetByIdAndTenantAsync(
            It.IsAny<DocumentId>(), It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);

        _storageMock.Setup(s => s.GetPageCdnUrlsAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(("https://cdn/full", "https://cdn/thumb", expiry));

        _cacheMock.Setup(c => c.SetPageUrlAsync(
            docId, 1, It.IsAny<PageUrlDto>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateHandler().HandleAsync(query);

        result.FullUrl.Should().Be("https://cdn/full");
        result.ThumbnailUrl.Should().Be("https://cdn/thumb");
        result.PageNumber.Should().Be(1);

        _cacheMock.Verify(c => c.SetPageUrlAsync(
            docId, 1, It.IsAny<PageUrlDto>(),
            TimeSpan.FromMinutes(55),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
