using System.Text.Json;
using Azure.Messaging.ServiceBus;
using DocumentProcessing.Core.Interfaces;
using DocumentProcessing.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Serilog;

namespace DocumentProcessing.Infrastructure.Messaging;

/// <summary>Azure Service Bus implementation of <see cref="IMessagePublisher"/>.</summary>
internal sealed class ServiceBusPublisher : IMessagePublisher, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusOptions _options;
    private static readonly ILogger Logger = Log.ForContext<ServiceBusPublisher>();

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public ServiceBusPublisher(ServiceBusClient client, IOptions<ServiceBusOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    /// <inheritdoc/>
    public async Task PublishAsync<T>(string topicOrQueueName, T message, string correlationId, CancellationToken cancellationToken = default)
        where T : class
    {
        var sender = _client.CreateSender(topicOrQueueName);
        await using var _ = sender.ConfigureAwait(false);

        var json = JsonSerializer.Serialize(message, SerializerOptions);
        var sbMessage = new ServiceBusMessage(json)
        {
            ContentType = "application/json",
            CorrelationId = correlationId,
            MessageId = Guid.NewGuid().ToString()
        };

        sbMessage.ApplicationProperties["MessageType"] = typeof(T).Name;

        await sender.SendMessageAsync(sbMessage, cancellationToken);

        Logger.Information(
            "Published {MessageType} to {Destination}, CorrelationId {CorrelationId}",
            typeof(T).Name, topicOrQueueName, correlationId);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
    }
}
