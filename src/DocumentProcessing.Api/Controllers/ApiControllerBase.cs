using DocumentProcessing.Core.CQRS;
using Microsoft.AspNetCore.Mvc;

namespace DocumentProcessing.Api.Controllers;

/// <summary>
/// Base controller that injects the CQRS dispatchers and provides strongly-typed
/// ActionResult helper methods to keep controller actions concise.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Dispatcher for commands.</summary>
    protected ICommandDispatcher Commands { get; }

    /// <summary>Dispatcher for queries.</summary>
    protected IQueryDispatcher Queries { get; }

    /// <summary>Correlation ID set by <see cref="Middleware.CorrelationIdMiddleware"/>.</summary>
    protected string CorrelationId =>
        HttpContext.Items["CorrelationId"]?.ToString() ?? string.Empty;

    /// <summary>Tenant identifier from the request header.</summary>
    protected string TenantId =>
        HttpContext.Request.Headers["X-Tenant-ID"].FirstOrDefault() ?? string.Empty;

    /// <inheritdoc/>
    protected ApiControllerBase(ICommandDispatcher commands, IQueryDispatcher queries)
    {
        Commands = commands;
        Queries = queries;
    }

    /// <summary>Returns <c>200 OK</c> with <paramref name="value"/>.</summary>
    protected new ActionResult<T> Ok<T>(T value) => base.Ok(value);

    /// <summary>Returns <c>201 Created</c> at <paramref name="uri"/> with <paramref name="value"/>.</summary>
    protected ActionResult<T> Created<T>(string uri, T value) => base.Created(uri, value);

    /// <summary>Returns a <c>400/404/422/409</c> ProblemDetails response.</summary>
    protected ActionResult ProblemResult(int statusCode, string title, string detail) =>
        Problem(detail: detail, title: title, statusCode: statusCode);
}
