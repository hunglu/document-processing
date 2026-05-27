using System.Diagnostics;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using DocumentProcessing.Contracts.Enums;
using DocumentProcessing.Contracts.Events;
using DocumentProcessing.Core.Commands;
using DocumentProcessing.Core.CQRS;
using DocumentProcessing.Core.Interfaces;
using DocumentProcessing.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace DocumentProcessing.Worker;

/// <summary>
/// Long-running hosted service that consumes messages from the document-processing-queue
/// and orchestrates the full PDF rendering pipeline.
/// </summary>
public sealed class ServiceBusWorker : BackgroundService
{
    private readonly ServiceBusClient _client;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DocumentProcessingPipeline _pipeline;
    private readonly ServiceBusOptions _sbOptions;
    private readonly ILogger<ServiceBusWorker> _logger;
    private static readonly ActivitySource ActivitySource = new("DocumentProcessing.Worker");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ResiliencePipeline _retryPolicy;

    public ServiceBusWorker(
        ServiceBusClient client,
        IServiceScopeFactory scopeFactory,
        DocumentProcessingPipeline pipeline,
        IOptions<ServiceBusOptions> sbOptions,
        ILogger<ServiceBusWorker> logger)
    {
        _client = client;
        _scopeFactory = scopeFactory;
        _pipeline = pipeline;
        _sbOptions = sbOptions.Value;
        _logger = logger;

        _retryPolicy = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                OnRetry = args =>
                {
                    _logger.LogWarning("Retry {Attempt} after {Delay}ms: {Exception}",
                        args.AttemptNumber, args.RetryDelay.TotalMilliseconds, args.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var processor = _client.CreateProcessor(
            _sbOptions.ProcessingQueueName,
            new ServiceBusProcessorOptions
            {
                MaxConcurrentCalls = 2,
                AutoCompleteMessages = false,
                MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(10)
            });

        processor.ProcessMessageAsync += ProcessMessageAsync;
        processor.ProcessErrorAsync += ProcessErrorAsync;

        await processor.StartProcessingAsync(stoppingToken);

        _logger.LogInformation("ServiceBusWorker listening on queue {Queue}", _sbOptions.ProcessingQueueName);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown
        }
        finally
        {
            await processor.StopProcessingAsync();
            await processor.DisposeAsync();
        }
    }

    private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        var correlationId = args.Message.CorrelationId ?? Guid.NewGuid().ToString("N");

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["MessageId"] = args.Message.MessageId
        }))
        {
            using var activity = ActivitySource.StartActivity("ProcessDocumentMessage");
            activity?.SetTag("correlation.id", correlationId);
            activity?.SetTag("message.id", args.Message.MessageId);

            try
            {
                var uploadedEvent = JsonSerializer.Deserialize<DocumentUploadedEvent>(
                    args.Message.Body.ToString(), JsonOptions)
                    ?? throw new InvalidOperationException("Failed to deserialize DocumentUploadedEvent.");

                _logger.LogInformation("Processing document {DocumentId} from tenant {TenantId}",
                    uploadedEvent.DocumentId, uploadedEvent.TenantId);

                await _retryPolicy.ExecuteAsync(async ct =>
                    await ProcessDocumentAsync(uploadedEvent, correlationId, ct),
                    args.CancellationToken);

                // Use CancellationToken.None so settle always completes even if the host
                // cancellation token has been signalled during a graceful shutdown.
                await args.CompleteMessageAsync(args.Message, CancellationToken.None);

                _logger.LogInformation("Message completed for document {DocumentId}", uploadedEvent.DocumentId);
            }
            catch (OperationCanceledException) when (args.CancellationToken.IsCancellationRequested)
            {
                // Host is shutting down — the processor will abandon the message automatically.
                _logger.LogWarning("Processing of message {MessageId} cancelled due to host shutdown; message will be abandoned",
                    args.Message.MessageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unrecoverable error processing message {MessageId}; dead-lettering", args.Message.MessageId);

                try
                {
                    await args.DeadLetterMessageAsync(
                        args.Message,
                        deadLetterReason: ex.GetType().Name,
                        deadLetterErrorDescription: ex.Message,
                        cancellationToken: CancellationToken.None);
                }
                catch (Exception dlEx)
                {
                    _logger.LogError(dlEx, "Failed to dead-letter message {MessageId}; message will be abandoned by the broker",
                        args.Message.MessageId);
                }
            }
        }
    }

    private async Task ProcessDocumentAsync(
        DocumentUploadedEvent uploadedEvent, string correlationId, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        await using var scope = _scopeFactory.CreateAsyncScope();
        var commandDispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();
        var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();
        var messagePublisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();
        var documentRepository = scope.ServiceProvider.GetRequiredService<Core.Interfaces.IDocumentRepository>();

        // 1. Transition to Processing
        await commandDispatcher.DispatchAsync(new UpdateDocumentStatusCommand
        {
            DocumentId = uploadedEvent.DocumentId,
            NewStatus = DocumentStatus.Processing,
            CorrelationId = correlationId
        }, cancellationToken);

        try
        {
            // 2. Download original PDF
            await using var pdfStream = await storageService.DownloadOriginalAsync(
                uploadedEvent.BlobPath, cancellationToken);

            // 3. Extract text from all pages in parallel via PdfPig
            var pages = await _pipeline.ProcessAsync(uploadedEvent.DocumentId, pdfStream, cancellationToken);

            // 5 & 6. Persist page records
            var document = await documentRepository.GetByIdAsync(
                new Core.ValueObjects.DocumentId(uploadedEvent.DocumentId), cancellationToken)
                ?? throw new InvalidOperationException($"Document {uploadedEvent.DocumentId} not found.");

            document.AddPages(pages);

            // 7. Transition to Ready
            await commandDispatcher.DispatchAsync(new UpdateDocumentStatusCommand
            {
                DocumentId = uploadedEvent.DocumentId,
                NewStatus = DocumentStatus.Ready,
                PageCount = pages.Count,
                CorrelationId = correlationId
            }, cancellationToken);

            await documentRepository.AddPagesAsync(pages, cancellationToken);

            sw.Stop();

            // Emit success event
            await messagePublisher.PublishAsync(
                _sbOptions.EventsTopicName,
                new DocumentProcessedEvent
                {
                    DocumentId = uploadedEvent.DocumentId,
                    TenantId = uploadedEvent.TenantId,
                    PageCount = pages.Count,
                    ProcessingDurationMs = sw.ElapsedMilliseconds,
                    OccurredAt = DateTimeOffset.UtcNow,
                    CorrelationId = correlationId
                },
                correlationId, cancellationToken);

            _logger.LogInformation(
                "Document {DocumentId} processed successfully: {PageCount} pages in {ElapsedMs}ms",
                uploadedEvent.DocumentId, pages.Count, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();

            var reason = ex switch
            {
                Azure.RequestFailedException rfe when rfe.Status == 404 => ProcessingFailureReason.BlobNotFound,
                // PdfPig throws PdfDocumentFormatException for corrupt/invalid PDFs
                { } e when e.GetType().Name.Contains("PdfDocumentFormat") => ProcessingFailureReason.InvalidPdf,
                _ => ProcessingFailureReason.Unknown
            };

            _logger.LogError(ex, "Document {DocumentId} processing failed: {Reason}", uploadedEvent.DocumentId, reason);

            // Update status → Failed
            await commandDispatcher.DispatchAsync(new UpdateDocumentStatusCommand
            {
                DocumentId = uploadedEvent.DocumentId,
                NewStatus = DocumentStatus.Failed,
                FailureReason = reason,
                FailureMessage = ex.Message,
                CorrelationId = correlationId
            }, cancellationToken);

            // Emit failure event
            await messagePublisher.PublishAsync(
                _sbOptions.EventsTopicName,
                new DocumentFailedEvent
                {
                    DocumentId = uploadedEvent.DocumentId,
                    TenantId = uploadedEvent.TenantId,
                    Reason = reason,
                    ErrorMessage = ex.Message,
                    OccurredAt = DateTimeOffset.UtcNow,
                    CorrelationId = correlationId
                },
                correlationId, cancellationToken);

            throw; // Allow Polly retry / dead-letter
        }
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception,
            "Service Bus error on {Source}/{Entity}: {ErrorSource}",
            args.FullyQualifiedNamespace, args.EntityPath, args.ErrorSource);
        return Task.CompletedTask;
    }
}
