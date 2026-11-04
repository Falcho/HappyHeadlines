using System.Diagnostics;
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using SubscriberService.Contracts;

namespace SubscriberService.Messaging;

public class SubscriberQueuePublisher
{
    public const string QueueName = "subscribers.welcome";

    private static readonly ActivitySource MessagingSource =
        new("HappyHeadlines.Messaging");

    private readonly IConnection _connection;

    public SubscriberQueuePublisher(IConnection connection)
    {
        _connection = connection;
    }

    public async Task PublishAsync(SubscriberCreated message)
    {
        using var activity = MessagingSource.StartActivity(
            "Queue new subscriber",
            ActivityKind.Producer);

        var channelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);

        await using var channel =
            await _connection.CreateChannelAsync(channelOptions);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            Persistent = true,
            MessageId = message.Id.ToString(),
            Headers = new Dictionary<string, object?>()
        };

        if (Activity.Current?.Id is string traceParent)
        {
            properties.Headers["traceparent"] =
                Encoding.UTF8.GetBytes(traceParent);
        }

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: QueueName,
            mandatory: true,
            basicProperties: properties,
            body: JsonSerializer.SerializeToUtf8Bytes(message));
    }
}
