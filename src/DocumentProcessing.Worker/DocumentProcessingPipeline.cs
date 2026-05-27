using System.Diagnostics;
using DocumentProcessing.Core.Domain;
using DocumentProcessing.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace DocumentProcessing.Worker;

/// <summary>
/// PdfPig-based PDF text extraction pipeline.
/// Opens each page, extracts plain text via word segmentation, and returns
/// <see cref="DocumentPage"/> records ready for database insertion.
/// </summary>
public sealed class DocumentProcessingPipeline
{
    private const int MaxConcurrentPages = 4;

    private readonly ILogger<DocumentProcessingPipeline> _logger;
    private static readonly ActivitySource ActivitySource = new("DocumentProcessing.Worker");

    public DocumentProcessingPipeline(ILogger<DocumentProcessingPipeline> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Extracts text from all pages of <paramref name="pdfStream"/> and returns
    /// <see cref="DocumentPage"/> records ready for database insertion.
    /// </summary>
    public async Task<IReadOnlyList<DocumentPage>> ProcessAsync(
        Guid documentId, Stream pdfStream, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("ProcessDocument");
        activity?.SetTag("document.id", documentId.ToString());

        _logger.LogInformation("Starting PDF text extraction for document {DocumentId}", documentId);

        var pdfBytes = await ReadAllBytesAsync(pdfStream, cancellationToken);

        using var pdf = PdfDocument.Open(pdfBytes);
        var pageCount = pdf.NumberOfPages;

        _logger.LogInformation("PDF has {PageCount} pages for document {DocumentId}", pageCount, documentId);
        activity?.SetTag("page.count", pageCount.ToString());

        var results = new DocumentPage[pageCount];
        var semaphore = new SemaphoreSlim(MaxConcurrentPages);
        var tasks = new Task[pageCount];

        for (var i = 0; i < pageCount; i++)
        {
            var pageIndex = i;
            tasks[i] = Task.Run(async () =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    results[pageIndex] = ExtractPageText(documentId, pdf, pageIndex + 1);
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken);
        }

        await Task.WhenAll(tasks);

        _logger.LogInformation("Text extracted from all {PageCount} pages for document {DocumentId}", pageCount, documentId);

        return results;
    }

    private DocumentPage ExtractPageText(Guid documentId, PdfDocument pdf, int pageNumber)
    {
        using var pageActivity = ActivitySource.StartActivity("ExtractPageText");
        pageActivity?.SetTag("document.id", documentId.ToString());
        pageActivity?.SetTag("page.number", pageNumber.ToString());

        _logger.LogDebug("Extracting text from page {PageNumber} for document {DocumentId}", pageNumber, documentId);

        var page = pdf.GetPage(pageNumber);
        var words = page.GetWords();
        var extractedText = string.Join(" ", words.Select(w => w.Text));

        _logger.LogDebug("Page {PageNumber} extracted {CharCount} chars for document {DocumentId}",
            pageNumber, extractedText.Length, documentId);

        return DocumentPage.Create(documentId, PageNumber.From(pageNumber), extractedText);
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        return ms.ToArray();
    }
}
