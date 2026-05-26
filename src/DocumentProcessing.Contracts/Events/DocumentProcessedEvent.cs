namespace DocumentProcessing.Contracts.Events;

/// <summary>Published when the worker finishes rendering all pages successfully.</summary>
public sealed class DocumentProcessedEvent
{
    /// <summary>Unique document identifier.</summary>
    public Guid DocumentId { get; init; }

    /// <summary>Tenant that owns the document.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Total number of pages rendered.</summary>
    public int PageCount { get; init; }

    /// <summary>Total processing duration in milliseconds.</summary>
    public long ProcessingDurationMs { get; init; }

    /// <summary>UTC timestamp when the event was emitted.</summary>
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>Correlation ID propagated from the originating service bus message.</summary>
    public string CorrelationId { get; init; } = string.Empty;
}
