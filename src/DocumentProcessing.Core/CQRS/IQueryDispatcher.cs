namespace DocumentProcessing.Core.CQRS;

/// <summary>Dispatches queries to their registered handlers via the DI container.</summary>
public interface IQueryDispatcher
{
    /// <summary>Dispatches a query and returns the result.</summary>
    Task<TResult> DispatchAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default);
}
