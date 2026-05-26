namespace DocumentProcessing.Core.CQRS;

/// <summary>Marker interface for commands that return a result of type <typeparamref name="TResult"/>.</summary>
/// <typeparam name="TResult">The type returned by the command handler.</typeparam>
public interface ICommand<TResult> { }

/// <summary>Marker interface for commands that produce no result.</summary>
public interface ICommand : ICommand<Unit> { }
