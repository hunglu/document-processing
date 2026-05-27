using DocumentProcessing.Core.CQRS;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DocumentProcessing.Infrastructure.CQRS;

/// <summary>
/// Resolves command handlers from the DI container at dispatch time.
/// Uses open-generic registration — no reflection scanning at startup.
/// </summary>
public class CommandDispatcher : ICommandDispatcher
{
    private readonly IServiceProvider _services;
    private readonly ILogger<CommandDispatcher> _logger;

    public CommandDispatcher(IServiceProvider services, ILogger<CommandDispatcher> logger)
    {
        _services = services;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<TResult> DispatchAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default)
    {
        var handlerType = typeof(ICommandHandler<,>).MakeGenericType(command.GetType(), typeof(TResult));
        var handler = _services.GetRequiredService(handlerType);

        _logger.LogDebug("Dispatching command {CommandType}", command.GetType().Name);

        var method = handlerType.GetMethod(nameof(ICommandHandler<ICommand<TResult>, TResult>.HandleAsync))!;
        return (Task<TResult>)method.Invoke(handler, [command, cancellationToken])!;
    }

    /// <inheritdoc/>
    public async Task DispatchAsync(ICommand command, CancellationToken cancellationToken = default)
    {
        var handlerType = typeof(ICommandHandler<>).MakeGenericType(command.GetType());
        var handler = _services.GetRequiredService(handlerType);

        _logger.LogDebug("Dispatching void command {CommandType}", command.GetType().Name);

        var method = handlerType.GetMethod(nameof(ICommandHandler<ICommand>.HandleAsync))!;
        await (Task)method.Invoke(handler, [command, cancellationToken])!;
    }
}
