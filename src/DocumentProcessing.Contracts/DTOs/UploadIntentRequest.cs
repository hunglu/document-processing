using System.ComponentModel.DataAnnotations;

namespace DocumentProcessing.Contracts.DTOs;

/// <summary>Request body for initiating a direct-to-blob upload intent.</summary>
public sealed class UploadIntentRequest
{
    /// <summary>Display file name provided by the client (e.g. "report.pdf").</summary>
    [Required]
    [MaxLength(256)]
    public string FileName { get; init; } = string.Empty;

    /// <summary>File size in bytes — used to validate the eventual upload.</summary>
    [Range(1, 524_288_000)] // 500 MB max
    public long FileSizeBytes { get; init; }

    /// <summary>SHA-256 checksum (hex, lowercase) of the file content, computed client-side.</summary>
    [Required]
    [RegularExpression("^[a-f0-9]{64}$", ErrorMessage = "Must be a lowercase hex SHA-256.")]
    public string Checksum { get; init; } = string.Empty;

    /// <summary>Caller-supplied tenant identifier.</summary>
    [Required]
    [MaxLength(64)]
    public string TenantId { get; init; } = string.Empty;
}
