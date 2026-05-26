namespace DocumentProcessing.Core.CQRS;

/// <summary>Handles a query of type <typeparamref name="TQuery"/> returning <typeparamref name="TResult"/>.</summary>
public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    /// <summary>Executes the query and returns the result.</summary>
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}
