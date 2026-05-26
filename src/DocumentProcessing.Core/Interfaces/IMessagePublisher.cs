namespace DocumentProcessing.Core.Interfaces;

/// <summary>Publishes messages to Azure Service Bus topics and queues.</summary>
public interface IMessagePublisher
{
    /// <summary>
    /// Publishes a message to the specified topic or queue name.
    /// The message is serialized to JSON and includes the correlation ID as a property.
    /// </summary>
    Task PublishAsync<T>(string topicOrQueueName, T message, string correlationId, CancellationToken cancellationToken = default) where T : class;
}
