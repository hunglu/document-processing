namespace DocumentProcessing.Contracts.DTOs;

/// <summary>Response to a successful upload-intent request; contains the SAS URL for direct upload.</summary>
public sealed class UploadIntentResponse
{
    /// <summary>Opaque identifier used to correlate the subsequent complete-upload call.</summary>
    public Guid UploadId { get; init; }

    /// <summary>Pre-signed Azure Blob SAS URL the client MUST PUT the PDF bytes to.</summary>
    public string SasUploadUrl { get; init; } = string.Empty;

    /// <summary>UTC timestamp after which <see cref="SasUploadUrl"/> is no longer valid.</summary>
    public DateTimeOffset SasExpiresAt { get; init; }
}
