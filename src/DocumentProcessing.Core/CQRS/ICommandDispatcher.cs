namespace DocumentProcessing.Core.CQRS;

/// <summary>Dispatches commands to their registered handlers via the DI container.</summary>
public interface ICommandDispatcher
{
    /// <summary>Dispatches a command that returns <typeparamref name="TResult"/>.</summary>
    Task<TResult> DispatchAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default);

    /// <summary>Dispatches a command that produces no result.</summary>
    Task DispatchAsync(ICommand command, CancellationToken cancellationToken = default);
}
