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
│ (GP_S_Gen5_2)  │  │  for Redis C1  │  │  (docs + pages)      │
└────────────────┘  └────────────────┘  └───────────┬──────────┘
                                                    │
                                          ┌─────────▼──────────┐
                                          │   Azure CDN Edge   │
                                          │ (cached WebP pages)│
                                          └────────────────────┘
         │ Service Bus Queue (document-processing-queue)
         ▼
┌────────────────────┐
│  DocumentProcessing│
│      .Worker       │  ← PdfPig, ImageSharp/WebP, Parallel.ForEachAsync
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

### Prerequisites

- Docker Desktop
- .NET 8 SDK
- Azure Service Bus namespace (or the Service Bus emulator from Microsoft)

### 1. Clone and configure

```bash
git clone <repo>
cd DocumentProcessing
cp .env.example .env
# Edit .env with your values (SQL_SA_PASSWORD is required)
```

### 2. Start all services

```bash
docker-compose up -d
```

Services started:
- `docproc-sqlserver` on `localhost:1433`
- `docproc-redis` on `localhost:6379`
- `docproc-azurite` on `localhost:10000` (blob)
- `docproc-datadog-agent` on `localhost:4317` (OTLP gRPC)
- `docproc-api` on `http://localhost:8080`
- `docproc-worker` (background service)

### 3. Apply EF Core migrations (first run only)

The API and Worker auto-migrate on startup in Development. To run manually:

```bash
dotnet ef database update \
  --project src/DocumentProcessing.Infrastructure \
  --startup-project src/DocumentProcessing.Api
```

### 4. Seed test data (optional)

```bash
# Upload a test PDF through the API
curl -X POST http://localhost:8080/api/v1/documents/upload-intent \
  -H "Content-Type: application/json" \
  -H "X-Tenant-ID: dev-tenant" \
  -d '{"fileName":"sample.pdf","fileSizeBytes":102400,"checksum":"'$(printf 'a%.0s' {1..64})'","tenantId":"dev-tenant"}'
```

### 5. Swagger UI

Open `http://localhost:8080/swagger` to explore the API.

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
