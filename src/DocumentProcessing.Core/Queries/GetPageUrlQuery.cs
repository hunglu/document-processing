using DocumentProcessing.Contracts.DTOs;
using DocumentProcessing.Core.CQRS;

namespace DocumentProcessing.Core.Queries;

/// <summary>Returns the CDN URLs for a single rendered page; checks Redis before hitting storage.</summary>
public sealed class GetPageUrlQuery : IQuery<PageUrlDto>
{
    /// <summary>Owning document.</summary>
    public Guid DocumentId { get; init; }

    /// <summary>1-based page number.</summary>
    public int PageNumber { get; init; }

    /// <summary>Calling tenant — enforces row-level security.</summary>
    public string TenantId { get; init; } = string.Empty;
}
