using System.Diagnostics;
using Serilog.Context;

namespace DocumentProcessing.Api.Middleware;

/// <summary>
/// Reads X-Correlation-ID from inbound requests (generating one if absent),
/// propagates it to Serilog's LogContext, adds it to OTEL baggage, and echoes it
/// back on every response.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-ID";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    /// <summary>Middleware entry point.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N");

        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers[CorrelationIdHeader] = correlationId;

        // Propagate to OTEL baggage so spans carry it
        Baggage.SetBaggage("correlation.id", correlationId);

        // Inject into current activity tags
        Activity.Current?.SetTag("correlation.id", correlationId);

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
