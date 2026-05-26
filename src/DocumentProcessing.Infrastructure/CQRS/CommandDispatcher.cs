using DocumentProcessing.Core.CQRS;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace DocumentProcessing.Infrastructure.CQRS;

/// <summary>
/// Resolves command handlers from the DI container at dispatch time.
/// Uses open-generic registration — no reflection scanning at startup.
/// </summary>
internal sealed class CommandDispatcher : ICommandDispatcher
{
    private readonly IServiceProvider _services;
    private static readonly ILogger Logger = Log.ForContext<CommandDispatcher>();

    public CommandDispatcher(IServiceProvider services) => _services = services;

    /// <inheritdoc/>
    public Task<TResult> DispatchAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default)
    {
        var handlerType = typeof(ICommandHandler<,>).MakeGenericType(command.GetType(), typeof(TResult));
        var handler = _services.GetRequiredService(handlerType);

        Logger.Debug("Dispatching command {CommandType}", command.GetType().Name);

        // Invoke HandleAsync via the concrete interface
        var method = handlerType.GetMethod(nameof(ICommandHandler<ICommand<TResult>, TResult>.HandleAsync))!;
        return (Task<TResult>)method.Invoke(handler, [command, cancellationToken])!;
    }

    /// <inheritdoc/>
    public async Task DispatchAsync(ICommand command, CancellationToken cancellationToken = default)
    {
        var handlerType = typeof(ICommandHandler<>).MakeGenericType(command.GetType());
        var handler = _services.GetRequiredService(handlerType);

        Logger.Debug("Dispatching void command {CommandType}", command.GetType().Name);

        var method = handlerType.GetMethod(nameof(ICommandHandler<ICommand>.HandleAsync))!;
        await (Task)method.Invoke(handler, [command, cancellationToken])!;
    }
}
