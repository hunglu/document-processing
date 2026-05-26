using DocumentProcessing.Contracts.Enums;

namespace DocumentProcessing.Contracts.Events;

/// <summary>Published when document processing fails after exhausting retries.</summary>
public sealed class DocumentFailedEvent
{
    /// <summary>Unique document identifier.</summary>
    public Guid DocumentId { get; init; }

    /// <summary>Tenant that owns the document.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Categorised failure reason.</summary>
    public ProcessingFailureReason Reason { get; init; }

    /// <summary>Exception message or descriptive error text.</summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>UTC timestamp when the event was emitted.</summary>
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>Correlation ID propagated from the originating service bus message.</summary>
    public string CorrelationId { get; init; } = string.Empty;
}
