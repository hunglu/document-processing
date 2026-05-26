using DocumentProcessing.Contracts.Events;
using DocumentProcessing.Core.Commands;
using DocumentProcessing.Core.Domain;
using DocumentProcessing.Core.Exceptions;
using DocumentProcessing.Core.Interfaces;
using DocumentProcessing.Core.ValueObjects;
using DocumentProcessing.Infrastructure.CQRS.Handlers;
using DocumentProcessing.Infrastructure.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace DocumentProcessing.UnitTests.Handlers;

public sealed class CompleteUploadCommandHandlerTests
{
    private readonly Mock<IDocumentRepository> _repositoryMock = new();
    private readonly Mock<IMessagePublisher> _publisherMock = new();

    private readonly IOptions<ServiceBusOptions> _sbOptions =
        Options.Create(new ServiceBusOptions
        {
            ProcessingQueueName = "document-processing-queue",
            EventsTopicName = "document-events"
        });

    private static readonly string ValidChecksum = new('a', 64);

    private CompleteUploadCommandHandler CreateHandler() =>
        new(_repositoryMock.Object, _publisherMock.Object, _sbOptions);

    private Document BuildUploadedDocument(Guid id, string checksum)
    {
        var doc = Document.CreateUploadIntent(
            new DocumentId(id),
            TenantId.From("t1"),
            "file.pdf",
            "t1/blob",
            Checksum.From(checksum),
            1024,
            "corr-setup");
        return doc;
    }

    [Fact]
    public async Task HandleAsync_ValidChecksum_TransitionsToUploaded_AndPublishesEvent()
    {
        var docId = Guid.NewGuid();
        var doc = BuildUploadedDocument(docId, ValidChecksum);

        _repositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<DocumentId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);
        _repositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _publisherMock.Setup(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<DocumentUploadedEvent>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new CompleteUploadCommand
        {
            UploadId = docId,
            Checksum = ValidChecksum,
            CorrelationId = "corr-complete"
        };

        var result = await CreateHandler().HandleAsync(command);

        result.Value.Should().Be(docId);

        _publisherMock.Verify(p => p.PublishAsync(
            "document-processing-queue",
            It.Is<DocumentUploadedEvent>(e => e.DocumentId == docId),
            "corr-complete",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ChecksumMismatch_ThrowsDomainException()
    {
        var docId = Guid.NewGuid();
        var doc = BuildUploadedDocument(docId, ValidChecksum);
        var differentChecksum = new string('b', 64);

        _repositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<DocumentId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);

        var command = new CompleteUploadCommand
        {
            UploadId = docId,
            Checksum = differentChecksum,
            CorrelationId = "corr"
        };

        var act = async () => await CreateHandler().HandleAsync(command);

        await act.Should().ThrowAsync<DocumentDomainException>().WithMessage("*Checksum mismatch*");
    }

    [Fact]
    public async Task HandleAsync_DocumentNotFound_ThrowsNotFoundException()
    {
        _repositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<DocumentId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Document?)null);

        var command = new CompleteUploadCommand
        {
            UploadId = Guid.NewGuid(),
            Checksum = ValidChecksum,
            CorrelationId = "corr"
        };

        var act = async () => await CreateHandler().HandleAsync(command);

        await act.Should().ThrowAsync<DocumentNotFoundException>();
    }
}
