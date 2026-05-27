using DocumentProcessing.Contracts.DTOs;
using DocumentProcessing.Core.CQRS;

namespace DocumentProcessing.Core.Commands;

/// <summary>Creates a new document record and generates a SAS URL for direct blob upload.</summary>
public class CreateUploadIntentCommand : ICommand<UploadIntentResponse>
{
    /// <summary>Display file name (e.g. "report.pdf").</summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>File size in bytes.</summary>
    public long FileSizeBytes { get; init; }

    /// <summary>SHA-256 checksum (hex) computed client-side.</summary>
    public string Checksum { get; init; } = string.Empty;

    /// <summary>Tenant identifier from the caller.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Correlation ID from the HTTP request.</summary>
    public string CorrelationId { get; init; } = string.Empty;
}
