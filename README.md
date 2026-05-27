# DocumentProcessing — Production-Ready .NET 8 PDF Processing System

## Architecture Diagram

```
Client Browser / Mobile App
        │
        │ HTTPS (TLS at CDN/App Gateway)
        ▼
┌────────────────────┐
│  DocumentProcessing│
│      .Api          │  ← Controller API, CQRS dispatch, Rate Limiting
│   (App Service)    │
└────────┬───────────┘
         │ EF Core 8         │ Redis ICacheService    │ Azure Blob SAS
         ▼                   ▼                         ▼
┌────────────────┐  ┌────────────────┐  ┌──────────────────────┐
│  Azure SQL DB  │  │  Azure Cache   │  │  Azure Blob Storage  │
│ (GP_S_Gen5_2)  │  │  for Redis C1  │  │  (original PDFs)     │
└────────────────┘  └────────────────┘  └────────────────────┘
         │ Service Bus Queue (document-processing-queue)
         ▼
┌────────────────────┐
│  DocumentProcessing│
│      .Worker       │  ← PdfPig text extraction, parallel per-page
│   (App Service)    │
└────────────────────┘
         │ Service Bus Topic (document-events)
         ▼
  Downstream consumers / webhooks

Observability cross-cuts everything:
  Serilog → Application Insights (structured logs)
  OpenTelemetry OTLP → DataDog Agent → DataDog APM (traces + metrics)
```

## How the 2-Second SLA Is Achieved

| Layer | Mechanism | Latency contribution |
|---|---|---|
| CDN Edge Cache | WebP pages cached at the Azure CDN POP nearest to the user | < 20 ms on cache hit |
| WebP format | ~70% smaller than PNG at same visual quality — faster transfer | Saves 100–400 ms on slow connections |
| Redis pre-signed URL cache | `GetPageUrlAsync` returns from Redis in < 1 ms on cache hit, skipping DB + storage | Avoids ~20–50 ms DB round-trip |
| SQL compiled queries | `EF.CompileAsyncQuery` eliminates LINQ tree compilation on the hot path | Saves ~5–10 ms per query |
| App Service P2v3 | 8 vCPUs / 32 GB RAM — no cold-start if always-on = true | Consistent < 100 ms API response |

On a cache miss the response time is still within 2 s because EF queries are async and the CDN revalidation is async-streamed.

## Local Development Setup

No Azure subscription required. Every Azure service is replaced by a local emulator.

| Azure service | Local replacement |
|---|---|
| Azure SQL | SQL Server 2022 (Docker) |
| Azure Blob Storage | Azurite (Docker) |
| Azure Service Bus | Service Bus Emulator (Docker) |
| Azure Cache for Redis | Redis 7 (Docker) |
| Application Insights | Dummy key — logs go to console only |
| DataDog | Skipped — OTLP errors are non-fatal, app still runs |

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) running
- .NET 8 SDK (`dotnet --version` → `8.x`)

---

### Step 1 — Create your `.env` file

```powershell
Copy-Item .env.example .env
```

The defaults in `.env.example` work as-is for local development.

---

### Step 2 — Start infrastructure

```powershell
docker-compose up sqlserver redis azurite servicebus-emulator -d
```

Wait ~30 seconds for SQL Server to finish initialising, then verify all containers are up:

```powershell
docker-compose ps
```

The Service Bus Emulator reads [`infra/local/servicebus-config.json`](infra/local/servicebus-config.json) on startup and automatically creates:

- Queue: `document-processing-queue`
- Topic: `document-events` (subscription: `all`)

---

### Step 3 — Run the API (Terminal 1)

```powershell
cd src/DocumentProcessing.Api
dotnet run
```

On first run the API auto-migrates the database (`ASPNETCORE_ENVIRONMENT=Development`). Look for:

```
[HH:mm:ss INF] Now listening on: http://localhost:5000
```

Swagger UI is available at `http://localhost:5000/swagger`.

---

### Step 4 — Run the Worker (Terminal 2)

```powershell
cd src/DocumentProcessing.Worker
dotnet run
```

Look for:

```
[HH:mm:ss INF] ServiceBusWorker listening on queue document-processing-queue
```

---

### Step 5 — Test the full processing flow

Have a small **text-based PDF** (any PDF with selectable text) saved as `sample.pdf` in your working directory.

#### 5a. Create an upload intent

```powershell
$response = Invoke-RestMethod -Method POST `
  -Uri "http://localhost:5000/api/v1/documents/upload-intent" `
  -ContentType "application/json" `
  -Headers @{ "X-Tenant-ID" = "tenant-1" } `
  -Body '{"fileName":"sample.pdf","fileSizeBytes":100000,"checksum":"abc123","tenantId":"tenant-1"}'

$uploadId = $response.uploadId
$sasUrl   = $response.sasUrl

Write-Host "Upload ID : $uploadId"
Write-Host "SAS URL   : $sasUrl"
```

#### 5b. Upload the PDF to Azurite via the SAS URL

```powershell
Invoke-RestMethod -Method PUT `
  -Uri $sasUrl `
  -Headers @{ "x-ms-blob-type" = "BlockBlob"; "Content-Type" = "application/pdf" } `
  -InFile ".\sample.pdf"
```

#### 5c. Signal upload complete (triggers the worker)

```powershell
Invoke-RestMethod -Method POST `
  -Uri "http://localhost:5000/api/v1/documents/$uploadId/complete" `
  -ContentType "application/json" `
  -Headers @{ "X-Tenant-ID" = "tenant-1" } `
  -Body '{"checksum":"abc123"}'
```

Worker terminal should immediately log:

```
[INF] Processing document <id> from tenant tenant-1
[INF] PDF has N pages for document <id>
[INF] Text extracted from all N pages for document <id>
[INF] Document <id> processed successfully: N pages in Xms
```

#### 5d. Poll processing status

```powershell
Invoke-RestMethod `
  -Uri "http://localhost:5000/api/v1/documents/$uploadId/status" `
  -Headers @{ "X-Tenant-ID" = "tenant-1" }
```

Expected response:

```json
{
  "documentId": "...",
  "status": "Ready",
  "pageCount": 3
}
```

#### 5e. Retrieve the manifest with extracted text

```powershell
Invoke-RestMethod `
  -Uri "http://localhost:5000/api/v1/documents/$uploadId/manifest" `
  -Headers @{ "X-Tenant-ID" = "tenant-1" } | ConvertTo-Json -Depth 5
```

Expected response:

```json
{
  "documentId": "...",
  "fileName": "sample.pdf",
  "pageCount": 3,
  "status": "Ready",
  "pages": [
    { "pageNumber": 1, "extractedText": "Introduction ..." },
    { "pageNumber": 2, "extractedText": "Section 2 ..." },
    { "pageNumber": 3, "extractedText": "Conclusion ..." }
  ]
}
```

#### 5f. Retrieve a single page

```powershell
Invoke-RestMethod `
  -Uri "http://localhost:5000/api/v1/documents/$uploadId/pages/1" `
  -Headers @{ "X-Tenant-ID" = "tenant-1" }
```

Second call to the same endpoint returns the same result from Redis cache (check the Worker logs for `Cache hit`).

---

### Manual migration (optional)

Migrations run automatically on startup. To run manually:

```powershell
dotnet ef database update `
  --project src/DocumentProcessing.Infrastructure `
  --startup-project src/DocumentProcessing.Api
```

---

### Troubleshooting

| Symptom | Fix |
|---|---|
| SQL Server container not healthy | Wait longer; check `docker logs docproc-sqlserver` |
| Service Bus Emulator exits immediately | SQL Server wasn't ready. Run `docker-compose restart servicebus-emulator` |
| `Redis:ConnectionString is required` at startup | `ASPNETCORE_ENVIRONMENT=Development` must be set — it is by default with `dotnet run` |
| OTLP exporter warnings in logs | Expected — DataDog is not running locally. Traces are dropped silently, the app is unaffected |
| SAS URL upload returns 400 | Use the SAS URL exactly as returned. Any modification (encoding, truncation) invalidates the signature |
| Worker status stays `Processing` | Check worker terminal for errors; re-run `docker-compose ps` to confirm the Service Bus Emulator is still up |

## Environment Variables Reference

| Variable | Required | Description | Example |
|---|---|---|---|
| `SQL_SA_PASSWORD` | Yes | SQL Server SA password | `Strong_Pass123!` |
| `SERVICEBUS_CONNECTION_STRING` | Yes (prod) | Azure Service Bus connection string | `Endpoint=sb://...` |
| `APPINSIGHTS_CONNECTION_STRING` | No | Application Insights connection | `InstrumentationKey=...` |
| `DD_API_KEY` | No | DataDog API key | `abc123...` |
| `DD_SITE` | No | DataDog site | `datadoghq.com` |
| `CDN_ENDPOINT_BASE_URL` | Yes (prod) | CDN base URL for pages | `https://mydocs.azureedge.net` |
| `Azure__BlobStorage__ConnectionString` | Yes | Blob storage connection | `DefaultEndpoints...` |
| `Redis__ConnectionString` | Yes | Redis connection string | `redis.cache.windows.net:6380,...` |
| `ConnectionStrings__DefaultConnection` | Yes | SQL Server connection | `Server=...` |

All production values should be stored in Azure Key Vault and referenced via
`@Microsoft.KeyVault(SecretUri=...)` in App Service Configuration — never hardcoded.

## Deployment Steps

### 1. Provision infrastructure

```bash
cd infra/terraform
terraform init
terraform plan -var="sql_admin_password=YourSecurePassword" -out=tfplan
terraform apply tfplan
```

### 2. Build and push Docker images

```bash
# Get ACR login server from Terraform output
ACR=$(terraform output -raw acr_login_server)
az acr login --name $ACR

# Build and push API
docker build -f Dockerfile.Api -t $ACR/docproc-api:$GITHUB_SHA .
docker push $ACR/docproc-api:$GITHUB_SHA

# Build and push Worker
docker build -f Dockerfile.Worker -t $ACR/docproc-worker:$GITHUB_SHA .
docker push $ACR/docproc-worker:$GITHUB_SHA
```

### 3. Deploy to App Service

```bash
# API
az webapp config container set \
  --name app-docproc-prod-api \
  --resource-group rg-document-processing \
  --docker-custom-image-name $ACR/docproc-api:$GITHUB_SHA

# Worker
az webapp config container set \
  --name app-docproc-prod-worker \
  --resource-group rg-document-processing \
  --docker-custom-image-name $ACR/docproc-worker:$GITHUB_SHA
```

### 4. Run EF migrations in production

Recommended: run as a job/init container before deploying the new image:

```bash
az webapp webjob continuous create \
  --name app-docproc-prod-api \
  --resource-group rg-document-processing \
  --webjob-name efmigrate \
  --webjob-type Continuous
```

Or use the `dotnet ef database update` command from a CI/CD pipeline step with the production connection string.

## Observability

### Application Insights Dashboard

Navigate to **Azure Portal → Application Insights → docproc-prod**:

- **Live Metrics**: real-time request rates, failure rates, CPU/memory
- **Transaction Search**: trace individual document upload flows by `CorrelationId`
- **Application Map**: dependency view (SQL, Redis, Service Bus)
- **Failures**: exceptions grouped by type — click-through to full stack trace

Key custom dimensions logged on every operation:
- `CorrelationId` — propagated from `X-Correlation-ID` HTTP header through to Service Bus messages
- `DocumentId` — attached on every handler span
- `TenantId` — for per-tenant filtering

### DataDog Service Map

Navigate to **DataDog → APM → Service Map**:

- `document-processing-api` and `document-processing-worker` appear as separate services
- Spans are automatically correlated via OTLP baggage (`correlation.id`)
- Custom `ActivitySource("DocumentProcessing.Infrastructure")` and `ActivitySource("DocumentProcessing.Worker")` produce named spans visible in the Trace view
- Redis, SQL, and HTTP client spans are auto-instrumented via OpenTelemetry

## Retention Compliance Summary

| Asset | Retention policy | Mechanism |
|---|---|---|
| Original PDF blobs (docs container) | Cool tier after 30 days, Archive after 180 days | Azure Blob lifecycle management policy |
| Rendered page images (pages container) | Standard hot tier | No auto-tier (pages are frequently read via CDN) |
| SQL audit entries | 7-year legal hold (append-only enforced in domain + EF) | Azure SQL long-term retention: 5-year yearly backup + append-only SaveChanges override |
| SQL point-in-time restore | 35 days | Azure SQL short-term retention policy |
| Service Bus messages | 7 days (configurable) | `default_message_time_to_live = "P7D"` |
| Application Insights telemetry | 90 days (default) | Configurable up to 730 days in workspace settings |

## Project Structure

```
DocumentProcessing/
├── src/
│   ├── DocumentProcessing.Contracts/    # DTOs, Events, Enums — no dependencies
│   ├── DocumentProcessing.Core/         # Domain, CQRS interfaces, value objects
│   ├── DocumentProcessing.Infrastructure/ # EF, Redis, Blob, SB, CQRS dispatchers
│   ├── DocumentProcessing.Api/          # ASP.NET Core controllers + middleware
│   └── DocumentProcessing.Worker/       # Background service + PDF pipeline
├── tests/
│   ├── DocumentProcessing.UnitTests/    # xUnit + Moq + FluentAssertions
│   └── DocumentProcessing.IntegrationTests/ # WebApplicationFactory + Testcontainers
├── infra/
│   └── terraform/                       # main.tf + variables.tf + outputs.tf
├── Dockerfile.Api
├── Dockerfile.Worker
├── docker-compose.yml
└── .env.example
```
