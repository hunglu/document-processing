using DocumentProcessing.Core.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DocumentProcessing.Api.Middleware;

/// <summary>
/// Catches unhandled exceptions and maps them to RFC 7807 ProblemDetails responses.
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>Middleware entry point.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DocumentNotFoundException ex)
        {
            _logger.LogWarning("Document not found: {Message}", ex.Message);
            await WriteProblemsAsync(context, StatusCodes.Status404NotFound, "Not Found", ex.Message);
        }
        catch (DocumentDomainException ex)
        {
            _logger.LogWarning("Domain rule violation: {Message}", ex.Message);
            await WriteProblemsAsync(context, StatusCodes.Status422UnprocessableEntity, "Domain Rule Violation", ex.Message);
        }
        catch (DocumentConflictException ex)
        {
            _logger.LogWarning("Concurrency conflict: {Message}", ex.Message);
            await WriteProblemsAsync(context, StatusCodes.Status409Conflict, "Conflict", ex.Message);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Bad request: {Message}", ex.Message);
            await WriteProblemsAsync(context, StatusCodes.Status400BadRequest, "Bad Request", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteProblemsAsync(context, StatusCodes.Status500InternalServerError,
                "Internal Server Error", "An unexpected error occurred. Please try again later.");
        }
    }

    private static async Task WriteProblemsAsync(HttpContext context, int status, string title, string detail)
    {
        if (context.Response.HasStarted) return;

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Type = $"https://httpstatuses.com/{status}",
            Title = title,
            Status = status,
            Detail = detail,
            Instance = context.Request.Path
        };

        if (context.Items["CorrelationId"] is string correlationId)
            problem.Extensions["correlationId"] = correlationId;

        await context.Response.WriteAsJsonAsync(problem);
    }
}
