namespace DocumentProcessing.Infrastructure.Configuration;

/// <summary>Typed options for Azure Service Bus configuration.</summary>
public sealed class ServiceBusOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Azure:ServiceBus";

    /// <summary>Service Bus namespace connection string.</summary>
    public string ConnectionString { get; init; } = string.Empty;

    /// <summary>Queue name for document processing work items.</summary>
    public string ProcessingQueueName { get; init; } = "document-processing-queue";

    /// <summary>Topic name for document lifecycle events.</summary>
    public string EventsTopicName { get; init; } = "document-events";
}
