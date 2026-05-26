using DocumentProcessing.Contracts.DTOs;
using DocumentProcessing.Core.Commands;
using DocumentProcessing.Core.CQRS;
using DocumentProcessing.Core.Queries;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;

namespace DocumentProcessing.Api.Controllers;

/// <summary>Manages document upload, status, and page-URL retrieval.</summary>
[EnableRateLimiting("per-tenant")]
public sealed class DocumentsController : ApiControllerBase
{
    private static readonly ILogger Logger = Log.ForContext<DocumentsController>();

    /// <inheritdoc/>
    public DocumentsController(ICommandDispatcher commands, IQueryDispatcher queries)
        : base(commands, queries) { }

    /// <summary>Initiates an upload intent and returns a SAS URL for direct client upload.</summary>
    /// <param name="request">Upload intent parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Upload ID and SAS URL.</returns>
    /// <response code="201">Upload intent created.</response>
    /// <response code="400">Validation failure.</response>
    [HttpPost("upload-intent")]
    [ProducesResponseType(typeof(UploadIntentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UploadIntentResponse>> CreateUploadIntent(
        [FromBody] UploadIntentRequest request,
        CancellationToken cancellationToken)
    {
        Logger.Information("Upload intent requested for {FileName} by tenant {TenantId}", request.FileName, TenantId);

        var command = new CreateUploadIntentCommand
        {
            FileName = request.FileName,
            FileSizeBytes = request.FileSizeBytes,
            Checksum = request.Checksum,
            TenantId = string.IsNullOrWhiteSpace(request.TenantId) ? TenantId : request.TenantId,
            CorrelationId = CorrelationId
        };

        var response = await Commands.DispatchAsync(command, cancellationToken);

        return Created($"/api/v1/documents/{response.UploadId}/status", response);
    }

    /// <summary>Confirms that the client has finished uploading bytes to the SAS URL.</summary>
    /// <param name="uploadId">The upload intent identifier returned from <see cref="CreateUploadIntent"/>.</param>
    /// <param name="request">Checksum of the uploaded file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The assigned document ID.</returns>
    /// <response code="202">Upload confirmed; processing enqueued.</response>
    /// <response code="404">Upload intent not found.</response>
    /// <response code="422">Checksum mismatch.</response>
    [HttpPost("{uploadId:guid}/complete")]
    [ProducesResponseType(typeof(object), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> CompleteUpload(
        [FromRoute] Guid uploadId,
        [FromBody] CompleteUploadRequest request,
        CancellationToken cancellationToken)
    {
        Logger.Information("Completing upload for {UploadId}", uploadId);

        var command = new CompleteUploadCommand
        {
            UploadId = uploadId,
            Checksum = request.Checksum,
            CorrelationId = CorrelationId
        };

        var documentId = await Commands.DispatchAsync(command, cancellationToken);

        return Accepted(new { documentId = documentId.Value });
    }

    /// <summary>Returns the current processing status of a document.</summary>
    /// <param name="documentId">Document identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Document status snapshot.</returns>
    /// <response code="200">Status returned.</response>
    /// <response code="404">Document not found.</response>
    [HttpGet("{documentId:guid}/status")]
    [ProducesResponseType(typeof(DocumentStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentStatusDto>> GetStatus(
        [FromRoute] Guid documentId,
        CancellationToken cancellationToken)
    {
        var query = new GetDocumentStatusQuery
        {
            DocumentId = documentId,
            TenantId = TenantId
        };

        var result = await Queries.DispatchAsync(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>Returns the CDN URLs for a specific rendered page.</summary>
    /// <param name="documentId">Document identifier.</param>
    /// <param name="pageNumber">1-based page number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Page CDN URLs.</returns>
    /// <response code="200">Page URLs returned (may be from Redis cache).</response>
    /// <response code="404">Document or page not found.</response>
    [HttpGet("{documentId:guid}/pages/{pageNumber:int}")]
    [ProducesResponseType(typeof(PageUrlDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PageUrlDto>> GetPageUrl(
        [FromRoute] Guid documentId,
        [FromRoute] int pageNumber,
        CancellationToken cancellationToken)
    {
        var query = new GetPageUrlQuery
        {
            DocumentId = documentId,
            PageNumber = pageNumber,
            TenantId = TenantId
        };

        var result = await Queries.DispatchAsync(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>Returns the complete document manifest including all page URLs.</summary>
    /// <param name="documentId">Document identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Document manifest with ordered page URLs.</returns>
    /// <response code="200">Manifest returned.</response>
    /// <response code="404">Document not found.</response>
    [HttpGet("{documentId:guid}/manifest")]
    [ProducesResponseType(typeof(DocumentManifestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentManifestDto>> GetManifest(
        [FromRoute] Guid documentId,
        CancellationToken cancellationToken)
    {
        var query = new GetDocumentManifestQuery
        {
            DocumentId = documentId,
            TenantId = TenantId
        };

        var result = await Queries.DispatchAsync(query, cancellationToken);
        return Ok(result);
    }
}
