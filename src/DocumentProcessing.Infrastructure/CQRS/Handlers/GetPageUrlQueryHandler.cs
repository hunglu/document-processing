using System.Diagnostics;
using DocumentProcessing.Contracts.DTOs;
using DocumentProcessing.Core.CQRS;
using DocumentProcessing.Core.Exceptions;
using DocumentProcessing.Core.Interfaces;
using DocumentProcessing.Core.Queries;
using DocumentProcessing.Core.ValueObjects;
using DocumentProcessing.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentProcessing.Infrastructure.CQRS.Handlers;

/// <summary>
/// Handles <see cref="GetPageUrlQuery"/>: checks Redis first; on miss, loads extracted text from DB and caches it.
/// </summary>
public class GetPageUrlQueryHandler : IQueryHandler<GetPageUrlQuery, PageUrlDto>
{
    private readonly IDocumentRepository _repository;
    private readonly ICacheService _cache;
    private readonly RedisOptions _redisOptions;
    private readonly ILogger<GetPageUrlQueryHandler> _logger;
    private static readonly ActivitySource ActivitySource = new("DocumentProcessing.Infrastructure");

    public GetPageUrlQueryHandler(
        IDocumentRepository repository,
        ICacheService cache,
        IOptions<RedisOptions> redisOptions,
        ILogger<GetPageUrlQueryHandler> logger)
    {
        _repository = repository;
        _cache = cache;
        _redisOptions = redisOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<PageUrlDto> HandleAsync(GetPageUrlQuery query, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("GetPageContent");
        activity?.SetTag("document.id", query.DocumentId.ToString());
        activity?.SetTag("page.number", query.PageNumber.ToString());

        var cached = await _cache.GetPageUrlAsync(query.DocumentId, query.PageNumber, cancellationToken);
        if (cached is not null)
        {
            _logger.LogDebug("Cache hit for page {PageNumber} of document {DocumentId}", query.PageNumber, query.DocumentId);
            activity?.SetTag("cache.hit", "true");
            return cached;
        }

        activity?.SetTag("cache.hit", "false");
        _logger.LogDebug("Cache miss for page {PageNumber} of document {DocumentId}; fetching from DB", query.PageNumber, query.DocumentId);

        var document = await _repository.GetByIdAndTenantAsync(
            new DocumentId(query.DocumentId), TenantId.From(query.TenantId), cancellationToken)
            ?? throw new DocumentNotFoundException(new DocumentId(query.DocumentId));

        var page = document.Pages.FirstOrDefault(p => p.PageNumber == query.PageNumber)
            ?? throw new ArgumentOutOfRangeException(nameof(query.PageNumber),
                $"Page {query.PageNumber} not found on document {query.DocumentId}.");

        var dto = new PageUrlDto
        {
            PageNumber = page.PageNumber,
            ExtractedText = page.ExtractedText
        };

        var ttl = TimeSpan.FromMinutes(_redisOptions.PageUrlTtlMinutes);
        await _cache.SetPageUrlAsync(query.DocumentId, query.PageNumber, dto, ttl, cancellationToken);

        _logger.LogInformation("Page content cached for page {PageNumber} of document {DocumentId}",
            query.PageNumber, query.DocumentId);

        return dto;
    }
}
