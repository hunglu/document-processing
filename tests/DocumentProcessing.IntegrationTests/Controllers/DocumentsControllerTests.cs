using System.Net;
using System.Net.Http.Json;
using DocumentProcessing.Contracts.DTOs;
using DocumentProcessing.IntegrationTests.Fixtures;
using FluentAssertions;

namespace DocumentProcessing.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for <see cref="DocumentProcessing.Api.Controllers.DocumentsController"/>.
/// Uses real SQL Server, Redis, and Azurite via Testcontainers.
/// </summary>
[Collection("Integration")]
public sealed class DocumentsControllerTests : IClassFixture<IntegrationTestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public DocumentsControllerTests(IntegrationTestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Tenant-ID", "integration-tenant");
    }

    [Fact]
    public async Task CreateUploadIntent_Returns201WithSasUrl()
    {
        var request = new UploadIntentRequest
        {
            FileName = "test.pdf",
            FileSizeBytes = 10240,
            Checksum = new string('a', 64),
            TenantId = "integration-tenant"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/documents/upload-intent", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<UploadIntentResponse>();
        body.Should().NotBeNull();
        body!.UploadId.Should().NotBe(Guid.Empty);
        body.SasUploadUrl.Should().NotBeNullOrWhiteSpace();
        body.SasExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task GetStatus_ForNonExistentDocument_Returns404()
    {
        var response = await _client.GetAsync($"/api/v1/documents/{Guid.NewGuid()}/status");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CompleteUpload_ForNonExistentDocument_Returns404()
    {
        var request = new CompleteUploadRequest { Checksum = new string('b', 64) };
        var response = await _client.PostAsJsonAsync($"/api/v1/documents/{Guid.NewGuid()}/complete", request);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task FullUploadFlow_CreateIntent_GetStatus_ReturnsCorrectStatuses()
    {
        // 1. Create intent
        var intentRequest = new UploadIntentRequest
        {
            FileName = "flow-test.pdf",
            FileSizeBytes = 512,
            Checksum = new string('c', 64),
            TenantId = "integration-tenant"
        };

        var intentResponse = await _client.PostAsJsonAsync("/api/v1/documents/upload-intent", intentRequest);
        intentResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var intent = await intentResponse.Content.ReadFromJsonAsync<UploadIntentResponse>();
        intent.Should().NotBeNull();

        // 2. Get status — should be PendingUpload
        var statusResponse = await _client.GetAsync($"/api/v1/documents/{intent!.UploadId}/status");
        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var status = await statusResponse.Content.ReadFromJsonAsync<DocumentStatusDto>();
        status.Should().NotBeNull();
        status!.DocumentId.Should().Be(intent.UploadId);
        status.Status.Should().Be(Contracts.Enums.DocumentStatus.PendingUpload);
    }

    [Fact]
    public async Task PageUrl_ForNonReadyDocument_Returns404Or422()
    {
        // Create intent
        var intentRequest = new UploadIntentRequest
        {
            FileName = "pages-test.pdf",
            FileSizeBytes = 256,
            Checksum = new string('d', 64),
            TenantId = "integration-tenant"
        };
        var intentResponse = await _client.PostAsJsonAsync("/api/v1/documents/upload-intent", intentRequest);
        var intent = await intentResponse.Content.ReadFromJsonAsync<UploadIntentResponse>();

        // Try to get page URL on document with no pages
        var pageResponse = await _client.GetAsync($"/api/v1/documents/{intent!.UploadId}/pages/1");
        pageResponse.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task HealthEndpoint_Returns200()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateUploadIntent_WithInvalidChecksum_Returns400()
    {
        var request = new UploadIntentRequest
        {
            FileName = "bad.pdf",
            FileSizeBytes = 100,
            Checksum = "invalid-checksum",
            TenantId = "t1"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/documents/upload-intent", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
