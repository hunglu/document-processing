using DocumentProcessing.Core.CQRS;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DocumentProcessing.Infrastructure.CQRS;

/// <summary>Resolves query handlers from the DI container at dispatch time.</summary>
public class QueryDispatcher : IQueryDispatcher
{
    private readonly IServiceProvider _services;
    private readonly ILogger<QueryDispatcher> _logger;

    public QueryDispatcher(IServiceProvider services, ILogger<QueryDispatcher> logger)
    {
        _services = services;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<TResult> DispatchAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
    {
        var handlerType = typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(TResult));
        var handler = _services.GetRequiredService(handlerType);

        _logger.LogDebug("Dispatching query {QueryType}", query.GetType().Name);

        var method = handlerType.GetMethod(nameof(IQueryHandler<IQuery<TResult>, TResult>.HandleAsync))!;
        return (Task<TResult>)method.Invoke(handler, [query, cancellationToken])!;
    }
}
