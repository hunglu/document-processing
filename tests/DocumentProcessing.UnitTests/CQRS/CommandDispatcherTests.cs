using DocumentProcessing.Core.CQRS;
using DocumentProcessing.Infrastructure.CQRS;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DocumentProcessing.UnitTests.CQRS;

public sealed class CommandDispatcherTests
{
    private sealed class TestCommand : ICommand<string> { public string Payload { get; init; } = string.Empty; }

    private sealed class TestCommandHandler : ICommandHandler<TestCommand, string>
    {
        public Task<string> HandleAsync(TestCommand command, CancellationToken cancellationToken = default)
            => Task.FromResult(command.Payload.ToUpperInvariant());
    }

    private sealed class ThrowingCommand : ICommand<string> { }

    private sealed class ThrowingCommandHandler : ICommandHandler<ThrowingCommand, string>
    {
        public Task<string> HandleAsync(ThrowingCommand command, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("handler error");
    }

    private static CommandDispatcher BuildDispatcher(Action<IServiceCollection> register)
    {
        var services = new ServiceCollection();
        services.AddLogging(); // Ensure logging is registered
        register(services);
        var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<CommandDispatcher>>();
        return new CommandDispatcher(provider, logger);
    }

    [Fact]
    public async Task DispatchAsync_ResolvesHandler_AndReturnsResult()
    {
        var dispatcher = BuildDispatcher(s =>
            s.AddScoped<ICommandHandler<TestCommand, string>, TestCommandHandler>());

        var result = await dispatcher.DispatchAsync(new TestCommand { Payload = "hello" });

        result.Should().Be("HELLO");
    }

    [Fact]
    public async Task DispatchAsync_PropagatesHandlerException()
    {
        var dispatcher = BuildDispatcher(s =>
            s.AddScoped<ICommandHandler<ThrowingCommand, string>, ThrowingCommandHandler>());

        var act = async () => await dispatcher.DispatchAsync(new ThrowingCommand());

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("handler error");
    }

    [Fact]
    public async Task DispatchAsync_ThrowsWhenHandlerNotRegistered()
    {
        var dispatcher = BuildDispatcher(_ => { });

        var act = async () => await dispatcher.DispatchAsync(new TestCommand());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
