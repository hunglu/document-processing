using DocumentProcessing.Contracts.Enums;

namespace DocumentProcessing.Core.Domain;

/// <summary>Immutable append-only audit record capturing each status transition on a document.</summary>
public sealed class AuditEntry
{
    private AuditEntry() { }

    /// <summary>Surrogate primary key.</summary>
    public long Id { get; private set; }

    /// <summary>Document that transitioned.</summary>
    public Guid DocumentId { get; private set; }

    /// <summary>Status before the transition.</summary>
    public DocumentStatus FromStatus { get; private set; }

    /// <summary>Status after the transition.</summary>
    public DocumentStatus ToStatus { get; private set; }

    /// <summary>Optional human-readable context (e.g. failure message).</summary>
    public string? Message { get; private set; }

    /// <summary>Identity that triggered the transition — "system" for automated transitions.</summary>
    public string Actor { get; private set; } = "system";

    /// <summary>Correlation ID from the triggering request or message.</summary>
    public string CorrelationId { get; private set; } = string.Empty;

    /// <summary>UTC timestamp of the transition.</summary>
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Creates a new audit entry for a status transition.</summary>
    public static AuditEntry Create(
        Guid documentId,
        DocumentStatus fromStatus,
        DocumentStatus toStatus,
        string correlationId,
        string actor = "system",
        string? message = null)
    {
        return new AuditEntry
        {
            DocumentId = documentId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            CorrelationId = correlationId,
            Actor = actor,
            Message = message,
            OccurredAt = DateTimeOffset.UtcNow
        };
    }
}
