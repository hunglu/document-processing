namespace DocumentProcessing.Contracts.DTOs;

/// <summary>Extracted text content for a single document page.</summary>
public sealed class PageUrlDto
{
    /// <summary>1-based page number.</summary>
    public int PageNumber { get; init; }

    /// <summary>Plain text extracted from the page by PdfPig.</summary>
    public string ExtractedText { get; init; } = string.Empty;
}
