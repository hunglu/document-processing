using DocumentProcessing.Contracts.Enums;
using DocumentProcessing.Core.Domain;
using DocumentProcessing.Core.Exceptions;
using DocumentProcessing.Core.ValueObjects;
using FluentAssertions;

namespace DocumentProcessing.UnitTests.Domain;

public sealed class DocumentTests
{
    private static Document CreateTestDocument(string correlationId = "test-correlation") =>
        Document.CreateUploadIntent(
            DocumentId.New(),
            TenantId.From("tenant-1"),
            "test.pdf",
            "tenant-1/doc-id/original/test.pdf",
            Checksum.From(new string('a', 64)),
            1024,
            correlationId);

    [Fact]
    public void CreateUploadIntent_Creates_DocumentInPendingUploadState()
    {
        var doc = CreateTestDocument();
        doc.Status.Should().Be(DocumentStatus.PendingUpload);
        doc.AuditEntries.Should().HaveCount(1);
        doc.Pages.Should().BeEmpty();
    }

    [Fact]
    public void MarkUploaded_FromPendingUpload_TransitionsToUploaded()
    {
        var doc = CreateTestDocument();
        doc.MarkUploaded("corr-1");
        doc.Status.Should().Be(DocumentStatus.Uploaded);
        doc.AuditEntries.Should().HaveCount(2);
    }

    [Fact]
    public void MarkProcessing_FromUploaded_TransitionsToProcessing()
    {
        var doc = CreateTestDocument();
        doc.MarkUploaded("corr-1");
        doc.MarkProcessing("corr-2");
        doc.Status.Should().Be(DocumentStatus.Processing);
    }

    [Fact]
    public void MarkReady_FromProcessing_TransitionsToReadyAndSetsPageCount()
    {
        var doc = CreateTestDocument();
        doc.MarkUploaded("corr-1");
        doc.MarkProcessing("corr-2");
        doc.MarkReady(10, "corr-3");
        doc.Status.Should().Be(DocumentStatus.Ready);
        doc.PageCount.Should().Be(10);
    }

    [Fact]
    public void MarkFailed_FromProcessing_TransitionsToFailed()
    {
        var doc = CreateTestDocument();
        doc.MarkUploaded("corr-1");
        doc.MarkProcessing("corr-2");
        doc.MarkFailed(ProcessingFailureReason.InvalidPdf, "corrupt pdf", "corr-3");
        doc.Status.Should().Be(DocumentStatus.Failed);
        doc.FailureReason.Should().Be(ProcessingFailureReason.InvalidPdf);
        doc.FailureMessage.Should().Be("corrupt pdf");
    }

    [Fact]
    public void MarkUploaded_FromProcessing_ThrowsDomainException()
    {
        var doc = CreateTestDocument();
        doc.MarkUploaded("corr-1");
        doc.MarkProcessing("corr-2");
        var act = () => doc.MarkUploaded("corr-3");
        act.Should().Throw<DocumentDomainException>()
           .WithMessage("*Processing*");
    }

    [Fact]
    public void MarkFailed_WhenAlreadyReady_ThrowsDomainException()
    {
        var doc = CreateTestDocument();
        doc.MarkUploaded("corr-1");
        doc.MarkProcessing("corr-2");
        doc.MarkReady(5, "corr-3");
        var act = () => doc.MarkFailed(ProcessingFailureReason.Unknown, "msg", "corr-4");
        act.Should().Throw<DocumentDomainException>();
    }

    [Fact]
    public void MarkReady_WithZeroPageCount_ThrowsArgumentOutOfRangeException()
    {
        var doc = CreateTestDocument();
        doc.MarkUploaded("c");
        doc.MarkProcessing("c");
        var act = () => doc.MarkReady(0, "c");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AddPages_WhenNotProcessing_ThrowsDomainException()
    {
        var doc = CreateTestDocument();
        var act = () => doc.AddPages([]);
        act.Should().Throw<DocumentDomainException>().WithMessage("*Processing*");
    }

    [Fact]
    public void AuditEntries_AreOrderedChronologically()
    {
        var doc = CreateTestDocument();
        doc.MarkUploaded("c1");
        doc.MarkProcessing("c2");

        var timestamps = doc.AuditEntries.Select(a => a.OccurredAt).ToList();
        timestamps.Should().BeInAscendingOrder();
    }
}
