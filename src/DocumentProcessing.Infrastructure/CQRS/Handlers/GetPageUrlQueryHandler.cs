using System.Diagnostics;
using DocumentProcessing.Contracts.DTOs;
using DocumentProcessing.Core.CQRS;
using DocumentProcessing.Core.Exceptions;
using DocumentProcessing.Core.Interfaces;
using DocumentProcessing.Core.Queries;
using DocumentProcessing.Core.ValueObjects;
using DocumentProcessing.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Serilog;

namespace DocumentProcessing.Infrastructure.CQRS.Handlers;

/// <summary>
/// Handles <see cref="GetPageUrlQuery"/>: checks Redis first; on miss, generates CDN URLs and caches them.
/// </summary>
internal sealed class GetPageUrlQueryHandler : IQueryHandler<GetPageUrlQuery, PageUrlDto>
{
    private readonly IDocumentRepository _repository;
    private readonly IStorageService _storage;
    private readonly ICacheService _cache;
    private readonly RedisOptions _redisOptions;
    private static readonly ILogger Logger = Log.ForContext<GetPageUrlQueryHandler>();
    private static readonly ActivitySource ActivitySource = new("DocumentProcessing.Infrastructure");

    public GetPageUrlQueryHandler(
        IDocumentRepository repository,
        IStorageService storage,
        ICacheService cache,
        IOptions<RedisOptions> redisOptions)
    {
        _repository = repository;
        _storage = storage;
        _cache = cache;
        _redisOptions = redisOptions.Value;
    }

    /// <inheritdoc/>
    public async Task<PageUrlDto> HandleAsync(GetPageUrlQuery query, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("GetPageUrl");
        activity?.SetTag("document.id", query.DocumentId.ToString());
        activity?.SetTag("page.number", query.PageNumber.ToString());

        // Redis hit path
        var cached = await _cache.GetPageUrlAsync(query.DocumentId, query.PageNumber, cancellationToken);
        if (cached is not null)
        {
            Logger.Debug("Cache hit for page {PageNumber} of document {DocumentId}", query.PageNumber, query.DocumentId);
            activity?.SetTag("cache.hit", "true");
            return cached;
        }

        activity?.SetTag("cache.hit", "false");
        Logger.Debug("Cache miss for page {PageNumber} of document {DocumentId}; fetching from DB", query.PageNumber, query.DocumentId);

        var document = await _repository.GetByIdAndTenantAsync(
            new DocumentId(query.DocumentId), TenantId.From(query.TenantId), cancellationToken)
            ?? throw new DocumentNotFoundException(new DocumentId(query.DocumentId));

        var page = document.Pages.FirstOrDefault(p => p.PageNumber == query.PageNumber)
            ?? throw new ArgumentOutOfRangeException(nameof(query.PageNumber),
                $"Page {query.PageNumber} not found on document {query.DocumentId}.");

        var (fullUrl, thumbUrl, expiresAt) = await _storage.GetPageCdnUrlsAsync(
            page.FullBlobPath, page.ThumbnailBlobPath, cancellationToken);

        var dto = new PageUrlDto
        {
            PageNumber = page.PageNumber,
            FullUrl = fullUrl,
            ThumbnailUrl = thumbUrl,
            ExpiresAt = expiresAt
        };

        var ttl = TimeSpan.FromMinutes(_redisOptions.PageUrlTtlMinutes);
        await _cache.SetPageUrlAsync(query.DocumentId, query.PageNumber, dto, ttl, cancellationToken);

        Logger.Information("Page URL generated and cached for page {PageNumber} of document {DocumentId}", query.PageNumber, query.DocumentId);

        return dto;
    }
}
