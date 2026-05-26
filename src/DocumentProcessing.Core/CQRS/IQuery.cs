namespace DocumentProcessing.Core.CQRS;

/// <summary>Marker interface for queries that return a result of type <typeparamref name="TResult"/>.</summary>
/// <typeparam name="TResult">The type returned by the query handler.</typeparam>
public interface IQuery<TResult> { }
