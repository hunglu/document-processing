# Document Processing System — Claude Code Project

## Project Overview

Large-scale construction plan PDF processing system. Users upload PDFs (5MB–2GB+),
the system splits them into pages, renders each page as WebP, and serves them via
Azure CDN with sub-2-second page render SLA.

## Scale Targets

- 50,000 active users
- ~2,000 document sets/hour at peak
- 7-year legal retention (immutable, auditable)
- Page render SLA: < 2 seconds in browser

## Tech Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 8 |
| API Style | ASP.NET Core Controller API |
| CQRS | Hand-rolled (no MediatR) — see patterns below |
| ORM | EF Core 8 (SQL Server) |
| Database | Azure SQL (SQL Server) |
| Blob Storage | Azure Blob Storage |
| CDN | Azure CDN (Front Door) |
| Messaging | Azure Service Bus (queues + topics) |
| Cache | Redis (Azure Cache for Redis) via StackExchange.Redis |
| Workers | .NET Worker Service |
| PDF Processing | PdfPig |
| Logging | Serilog → Azure Application Insights sink |
| Tracing | DataDog APM (OpenTelemetry → DD agent) |
| Auth | JWT Bearer (ICurrentUserService abstraction) |
| Testing | xUnit, FluentAssertions, NSubstitute, Testcontainers |
| IaC | Terraform (Azure provider) |
| Containers | Docker + docker-compose (Azurite for local blob/queue) |

## Solution Structure
DocumentProcessing/
├── src/
│   ├── DocumentProcessing.Api/           # Controller API
│   ├── DocumentProcessing.Worker/        # Azure Service Bus consumer
│   ├── DocumentProcessing.Core/          # Domain, CQRS interfaces, models
│   ├── DocumentProcessing.Infrastructure/# Azure, EF Core, Redis, Serilog, DD
│   └── DocumentProcessing.Contracts/     # DTOs, events, enums
├── tests/
│   ├── DocumentProcessing.UnitTests/
│   └── DocumentProcessing.IntegrationTests/
├── infra/                                # Terraform
└── docker-compose.yml

## CQRS Pattern (No MediatR)

Implement by hand. Do not install MediatR under any circumstances.

### Interfaces (DocumentProcessing.Core/CQRS/)

```csharp
public interface ICommand { }
public interface ICommand<TResult> { }
public interface IQuery<TResult> { }

public interface ICommandHandler<TCommand> where TCommand : ICommand
{
    Task HandleAsync(TCommand command, CancellationToken ct = default);
}

public interface ICommandHandler<TCommand, TResult> where TCommand : ICommand<TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken ct = default);
}

public interface IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken ct = default);
}
```

### Dispatcher (DocumentProcessing.Core/CQRS/)

```csharp
public interface ICommandDispatcher
{
    Task SendAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : ICommand;
    Task<TResult> SendAsync<TCommand, TResult>(TCommand command, CancellationToken ct = default)
        where TCommand : ICommand<TResult>;
}

public interface IQueryDispatcher
{
    Task<TResult> QueryAsync<TQuery, TResult>(TQuery query, CancellationToken ct = default)
        where TQuery : IQuery<TResult>;
}
```

Dispatcher implementations resolve handlers via `IServiceProvider`. Register all
handlers in DI by scanning assemblies — no source generators.

### Usage in Controllers

```csharp
// Command
await _commandDispatcher.SendAsync<CompleteUploadCommand, DocumentId>(command, ct);

// Query
var result = await _queryDispatcher.QueryAsync<GetDocumentStatusQuery, DocumentStatusDto>(query, ct);
```

## Logging — Serilog

- Sink: Azure Application Insights (TelemetryConfiguration)
- Enrich with: CorrelationId, MachineName, ThreadId, Environment
- Structured logging only — no string interpolation in log calls
- Minimum level: Information (prod), Debug (dev)
- Log request/response via middleware (exclude health endpoints)

```csharp
// Always use structured logging
Log.Information("Document {DocumentId} processing started for tenant {TenantId}", id, tenantId);
// Never
Log.Information($"Document {id} processing started"); // ❌
```

## Distributed Tracing — DataDog

- Use OpenTelemetry SDK with DataDog exporter
- Trace all: HTTP requests, Service Bus message handling, Redis ops, SQL queries, blob ops
- Custom spans for PDF page extraction loop
- Inject DD_SERVICE, DD_ENV, DD_VERSION via environment variables
- Resource attributes: service.name, deployment.environment
- No dd-trace-dotnet agent required in local dev; configure via OTEL_EXPORTER_OTLP_ENDPOINT

## Domain Model
Document          — aggregate root, owns state machine
DocumentPage      — entity, belongs to Document
AuditEntry        — append-only, immutable after creation

Document status lifecycle:
Pending → Processing → Ready
→ Failed (retryable)

Rich domain: Document.BeginProcessing(), Document.MarkPageReady(pageNumber),
Document.Complete(), Document.Fail(reason) — no anemic model.

## Azure Blob Storage Key Scheme
docs/{tenantId}/{documentId}/original.pdf
docs/{tenantId}/{documentId}/pages/{pageNumber}/thumb.webp   (150 DPI)
docs/{tenantId}/{documentId}/pages/{pageNumber}/full.webp    (300 DPI)

## Retention & Compliance

- Azure Blob immutability policy (time-based, 7 years, locked)
- SHA-256 checksum stored in DB + blob metadata on every upload
- AuditEntry table: DB role has INSERT only — no UPDATE/DELETE
- Lifecycle: tier to Cool after 30 days, Archive after 90 days

## API Conventions

- Versioned: /api/v1/...
- Controller base: ApiControllerBase with problem details on errors
- All endpoints return ProblemDetails on 4xx/5xx (RFC 7807)
- Correlation ID header: X-Correlation-ID (generate if absent, propagate to logs + traces)
- Rate limiting: sliding window per user (ASP.NET Core RateLimiter)
- Health: GET /health/live, GET /health/ready (checks SQL, Redis, Service Bus)

## Testing Standards

- Unit tests: pure domain logic, CQRS handlers (mock infrastructure)
- Integration tests: WebApplicationFactory + Testcontainers (SQL Server, Redis)
- Azurite for blob/Service Bus in local/CI
- No logic in controllers — thin controllers, fat handlers
- Aim for 80%+ coverage on Core + Infrastructure

## Local Dev

- docker-compose up starts: api, worker, sqlserver, redis, azurite, datadog-agent
- Azurite replaces Azure Blob + Service Bus locally
- Use dotnet user-secrets for connection strings
- Seed script creates initial DB schema via EF migrations

## Code Standards

- SOLID, clean architecture — no cross-layer leakage
- async/await throughout — no .Result or .Wait()
- IOptions<T> for all config sections
- Strongly-typed IDs: DocumentId, TenantId (record struct + EF value converter)
- XML doc comments on all public API surface
- Nullable reference types enabled globally
- No magic strings — use constants or enums
