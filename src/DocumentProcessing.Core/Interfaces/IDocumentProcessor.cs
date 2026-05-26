using DocumentProcessing.Core.Domain;

namespace DocumentProcessing.Core.Interfaces;

/// <summary>Contract for the PDF rendering pipeline — implemented in the Worker project using PdfPig.</summary>
public interface IDocumentProcessor
{
    /// <summary>
    /// Renders all pages of the PDF at <paramref name="pdfStream"/> as WebP images,
    /// uploads them to blob storage, and returns the resulting <see cref="DocumentPage"/> records.
    /// </summary>
    Task<IReadOnlyList<DocumentPage>> ProcessAsync(
        Guid documentId,
        Stream pdfStream,
        CancellationToken cancellationToken = default);
}
