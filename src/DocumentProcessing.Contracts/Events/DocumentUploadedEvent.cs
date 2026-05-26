namespace DocumentProcessing.Contracts.Events;

/// <summary>Published to Service Bus when a client confirms a completed upload.</summary>
public sealed class DocumentUploadedEvent
{
    /// <summary>Unique document identifier.</summary>
    public Guid DocumentId { get; init; }

    /// <summary>Tenant that owns the document.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Blob path of the original PDF in the docs container.</summary>
    public string BlobPath { get; init; } = string.Empty;

    /// <summary>Original file name.</summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>SHA-256 checksum of the original file.</summary>
    public string Checksum { get; init; } = string.Empty;

    /// <summary>UTC timestamp when the event was emitted.</summary>
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>Correlation ID propagated from the originating HTTP request.</summary>
    public string CorrelationId { get; init; } = string.Empty;
}
