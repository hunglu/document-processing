using System.ComponentModel.DataAnnotations;

namespace DocumentProcessing.Contracts.DTOs;

/// <summary>Request body sent after the client has finished PUT-ing bytes to the SAS URL.</summary>
public sealed class CompleteUploadRequest
{
    /// <summary>SHA-256 checksum as stored in blob metadata — must match the intent checksum.</summary>
    [Required]
    [RegularExpression("^[a-f0-9]{64}$", ErrorMessage = "Must be a lowercase hex SHA-256.")]
    public string Checksum { get; init; } = string.Empty;
}
