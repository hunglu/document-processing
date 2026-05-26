using DocumentProcessing.Core.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace DocumentProcessing.Api.Middleware;

/// <summary>
/// Catches unhandled exceptions and maps them to RFC 7807 ProblemDetails responses.
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly ILogger Logger = Log.ForContext<GlobalExceptionMiddleware>();

    public GlobalExceptionMiddleware(RequestDelegate next) => _next = next;

    /// <summary>Middleware entry point.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DocumentNotFoundException ex)
        {
            Logger.Warning("Document not found: {Message}", ex.Message);
            await WriteProblemsAsync(context, StatusCodes.Status404NotFound, "Not Found", ex.Message);
        }
        catch (DocumentDomainException ex)
        {
            Logger.Warning("Domain rule violation: {Message}", ex.Message);
            await WriteProblemsAsync(context, StatusCodes.Status422UnprocessableEntity, "Domain Rule Violation", ex.Message);
        }
        catch (DocumentConflictException ex)
        {
            Logger.Warning("Concurrency conflict: {Message}", ex.Message);
            await WriteProblemsAsync(context, StatusCodes.Status409Conflict, "Conflict", ex.Message);
        }
        catch (ArgumentException ex)
        {
            Logger.Warning("Bad request: {Message}", ex.Message);
            await WriteProblemsAsync(context, StatusCodes.Status400BadRequest, "Bad Request", ex.Message);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
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
