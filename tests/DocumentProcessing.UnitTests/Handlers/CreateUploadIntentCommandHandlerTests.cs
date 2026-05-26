using DocumentProcessing.Core.Commands;
using DocumentProcessing.Core.Interfaces;
using DocumentProcessing.Infrastructure.CQRS.Handlers;
using FluentAssertions;
using Moq;

namespace DocumentProcessing.UnitTests.Handlers;

public sealed class CreateUploadIntentCommandHandlerTests
{
    private readonly Mock<IDocumentRepository> _repositoryMock = new();
    private readonly Mock<IStorageService> _storageMock = new();

    private CreateUploadIntentCommandHandler CreateHandler() =>
        new(_repositoryMock.Object, _storageMock.Object);

    [Fact]
    public async Task HandleAsync_GeneratesSasUrl_AndPersistsDocument()
    {
        var expectedSasUrl = "https://blob.core.windows.net/docs/tenant1/doc123/file.pdf?sas=token";
        var expectedBlobPath = "tenant1/doc123/original/report.pdf";
        var expectedExpiry = DateTimeOffset.UtcNow.AddHours(1);

        _storageMock
            .Setup(s => s.GenerateSasUploadUrlAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((expectedSasUrl, expectedBlobPath, expectedExpiry));

        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Core.Domain.Document>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new CreateUploadIntentCommand
        {
            FileName = "report.pdf",
            FileSizeBytes = 2048,
            Checksum = new string('a', 64),
            TenantId = "tenant1",
            CorrelationId = "corr-test"
        };

        var result = await CreateHandler().HandleAsync(command);

        result.SasUploadUrl.Should().Be(expectedSasUrl);
        result.SasExpiresAt.Should().Be(expectedExpiry);
        result.UploadId.Should().NotBe(Guid.Empty);

        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<Core.Domain.Document>(), It.IsAny<CancellationToken>()), Times.Once);
        _storageMock.Verify(s => s.GenerateSasUploadUrlAsync("tenant1", It.IsAny<string>(), "report.pdf", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidChecksum_Throws()
    {
        var command = new CreateUploadIntentCommand
        {
            FileName = "f.pdf",
            FileSizeBytes = 100,
            Checksum = "not-a-valid-checksum",
            TenantId = "t1",
            CorrelationId = "c"
        };

        var act = async () => await CreateHandler().HandleAsync(command);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
