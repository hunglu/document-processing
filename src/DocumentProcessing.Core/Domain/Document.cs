using DocumentProcessing.Contracts.Enums;
using DocumentProcessing.Core.Exceptions;
using DocumentProcessing.Core.ValueObjects;

namespace DocumentProcessing.Core.Domain;

/// <summary>
/// Document aggregate root.  Encapsulates the lifecycle state machine and enforces
/// all invariants — no state changes are permitted outside this class.
/// </summary>
public sealed class Document
{
    private readonly List<DocumentPage> _pages = [];
    private readonly List<AuditEntry> _auditEntries = [];

    private Document() { }

    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>Primary key (maps to DocumentId value object).</summary>
    public Guid Id { get; private set; }

    /// <summary>Tenant that owns this document.</summary>
    public string TenantId { get; private set; } = string.Empty;

    // ── Metadata ──────────────────────────────────────────────────────────────

    /// <summary>Original file name supplied by the uploader.</summary>
    public string FileName { get; private set; } = string.Empty;

    /// <summary>Blob path of the original PDF in the docs container.</summary>
    public string BlobPath { get; private set; } = string.Empty;

    /// <summary>SHA-256 checksum of the original file.</summary>
    public string Checksum { get; private set; } = string.Empty;

    /// <summary>File size in bytes.</summary>
    public long FileSizeBytes { get; private set; }

    /// <summary>Number of pages; null until processing begins.</summary>
    public int? PageCount { get; private set; }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <summary>Current processing status.</summary>
    public DocumentStatus Status { get; private set; }

    /// <summary>Reason for failure when <see cref="Status"/> is <see cref="DocumentStatus.Failed"/>.</summary>
    public ProcessingFailureReason? FailureReason { get; private set; }

    /// <summary>Error message when <see cref="Status"/> is <see cref="DocumentStatus.Failed"/>.</summary>
    public string? FailureMessage { get; private set; }

    /// <summary>UTC timestamp when the record was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>UTC timestamp of the last status change — used as optimistic concurrency token.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>EF Core row version for optimistic concurrency.</summary>
    public byte[]? RowVersion { get; private set; }

    // ── Navigation ────────────────────────────────────────────────────────────

    /// <summary>Rendered pages; empty until status reaches <see cref="DocumentStatus.Ready"/>.</summary>
    public IReadOnlyList<DocumentPage> Pages => _pages.AsReadOnly();

    /// <summary>Append-only audit trail.</summary>
    public IReadOnlyList<AuditEntry> AuditEntries => _auditEntries.AsReadOnly();

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>Creates a new document in <see cref="DocumentStatus.PendingUpload"/> state.</summary>
    public static Document CreateUploadIntent(
        DocumentId id,
        TenantId tenantId,
        string fileName,
        string blobPath,
        Checksum checksum,
        long fileSizeBytes,
        string correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(blobPath);
        if (fileSizeBytes <= 0) throw new ArgumentOutOfRangeException(nameof(fileSizeBytes));

        var now = DateTimeOffset.UtcNow;
        var doc = new Document
        {
            Id = id.Value,
            TenantId = tenantId.Value,
            FileName = fileName,
            BlobPath = blobPath,
            Checksum = checksum.Value,
            FileSizeBytes = fileSizeBytes,
            Status = DocumentStatus.PendingUpload,
            CreatedAt = now,
            UpdatedAt = now
        };

        doc._auditEntries.Add(AuditEntry.Create(
            doc.Id, DocumentStatus.PendingUpload, DocumentStatus.PendingUpload,
            correlationId, message: "Upload intent created"));

        return doc;
    }

    // ── State transitions ─────────────────────────────────────────────────────

    /// <summary>Marks the document as uploaded; triggers queuing for processing.</summary>
    public void MarkUploaded(string correlationId)
    {
        EnsureTransitionAllowed(DocumentStatus.PendingUpload, DocumentStatus.Uploaded);
        Transition(DocumentStatus.Uploaded, correlationId);
    }

    /// <summary>Marks the document as being actively processed by the worker.</summary>
    public void MarkProcessing(string correlationId)
    {
        EnsureTransitionAllowed(DocumentStatus.Uploaded, DocumentStatus.Processing);
        Transition(DocumentStatus.Processing, correlationId);
    }

    /// <summary>Marks the document ready after all pages are rendered and persisted.</summary>
    public void MarkReady(int pageCount, string correlationId)
    {
        EnsureTransitionAllowed(DocumentStatus.Processing, DocumentStatus.Ready);
        if (pageCount < 1) throw new ArgumentOutOfRangeException(nameof(pageCount));
        PageCount = pageCount;
        Transition(DocumentStatus.Ready, correlationId);
    }

    /// <summary>Marks the document failed; records the reason and message.</summary>
    public void MarkFailed(ProcessingFailureReason reason, string message, string correlationId)
    {
        if (Status == DocumentStatus.Ready)
            throw new DocumentDomainException($"Cannot fail a document that is already Ready. DocumentId={Id}");

        FailureReason = reason;
        FailureMessage = message;
        var from = Status;
        UpdatedAt = DateTimeOffset.UtcNow;
        Status = DocumentStatus.Failed;

        _auditEntries.Add(AuditEntry.Create(Id, from, DocumentStatus.Failed, correlationId,
            message: $"{reason}: {message}"));
    }

    /// <summary>Appends page records; must only be called during the Processing → Ready transition.</summary>
    public void AddPages(IEnumerable<DocumentPage> pages)
    {
        if (Status != DocumentStatus.Processing)
            throw new DocumentDomainException($"Pages can only be added while Processing. Status={Status}");
        _pages.AddRange(pages);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void EnsureTransitionAllowed(DocumentStatus expected, DocumentStatus target)
    {
        if (Status != expected)
            throw new DocumentDomainException(
                $"Cannot transition to {target} from {Status}. Expected {expected}. DocumentId={Id}");
    }

    private void Transition(DocumentStatus to, string correlationId)
    {
        var from = Status;
        Status = to;
        UpdatedAt = DateTimeOffset.UtcNow;
        _auditEntries.Add(AuditEntry.Create(Id, from, to, correlationId));
    }
}
