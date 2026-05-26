namespace DocumentProcessing.Core.CQRS;

/// <summary>Handles a command of type <typeparamref name="TCommand"/> producing <typeparamref name="TResult"/>.</summary>
public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    /// <summary>Executes the command and returns the result.</summary>
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

/// <summary>Handles a command that produces no result.</summary>
public interface ICommandHandler<in TCommand>
    where TCommand : ICommand
{
    /// <summary>Executes the command.</summary>
    Task HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}
