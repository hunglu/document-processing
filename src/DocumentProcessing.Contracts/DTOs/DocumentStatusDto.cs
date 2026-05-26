using DocumentProcessing.Contracts.Enums;

namespace DocumentProcessing.Contracts.DTOs;

/// <summary>Snapshot of a document's processing status returned by the status endpoint.</summary>
public sealed class DocumentStatusDto
{
    /// <summary>Unique document identifier.</summary>
    public Guid DocumentId { get; init; }

    /// <summary>Current lifecycle state.</summary>
    public DocumentStatus Status { get; init; }

    /// <summary>Number of pages detected. Null until processing begins.</summary>
    public int? PageCount { get; init; }

    /// <summary>UTC timestamp when the document record was created.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>UTC timestamp of the last status transition.</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Failure reason when <see cref="Status"/> is <see cref="DocumentStatus.Failed"/>; null otherwise.</summary>
    public ProcessingFailureReason? FailureReason { get; init; }

    /// <summary>Human-readable failure message; populated only when failed.</summary>
    public string? FailureMessage { get; init; }
}
