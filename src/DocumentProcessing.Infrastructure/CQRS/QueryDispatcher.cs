using DocumentProcessing.Core.CQRS;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace DocumentProcessing.Infrastructure.CQRS;

/// <summary>Resolves query handlers from the DI container at dispatch time.</summary>
internal sealed class QueryDispatcher : IQueryDispatcher
{
    private readonly IServiceProvider _services;
    private static readonly ILogger Logger = Log.ForContext<QueryDispatcher>();

    public QueryDispatcher(IServiceProvider services) => _services = services;

    /// <inheritdoc/>
    public Task<TResult> DispatchAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
    {
        var handlerType = typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(TResult));
        var handler = _services.GetRequiredService(handlerType);

        Logger.Debug("Dispatching query {QueryType}", query.GetType().Name);

        var method = handlerType.GetMethod(nameof(IQueryHandler<IQuery<TResult>, TResult>.HandleAsync))!;
        return (Task<TResult>)method.Invoke(handler, [query, cancellationToken])!;
    }
}
