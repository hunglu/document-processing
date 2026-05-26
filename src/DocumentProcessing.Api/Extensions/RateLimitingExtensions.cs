using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace DocumentProcessing.Api.Extensions;

/// <summary>Rate limiting setup for the API.</summary>
public static class RateLimitingExtensions
{
    /// <summary>
    /// Adds a sliding window rate limiter: 100 requests per minute per user (keyed on tenant header).
    /// </summary>
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddSlidingWindowLimiter("per-tenant", opt =>
            {
                opt.PermitLimit = 100;
                opt.Window = TimeSpan.FromMinutes(1);
                opt.SegmentsPerWindow = 6; // 10-second resolution
                opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opt.QueueLimit = 10;
            });

            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.Headers.RetryAfter = "60";
                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    type = "https://tools.ietf.org/html/rfc6585#section-4",
                    title = "Too Many Requests",
                    status = 429,
                    detail = "Rate limit exceeded. Retry after 60 seconds."
                }, token);
            };
        });

        return services;
    }
}
