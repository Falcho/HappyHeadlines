using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NewsletterService.Contracts;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NewsletterService.Messaging;

public class WelcomeMailConsumer : BackgroundService
{
    private const string QueueName = "subscribers.welcome";

    private static readonly ActivitySource MessagingSource =
        new("HappyHeadlines.Messaging");

    private readonly IConnection _connection;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<WelcomeMailConsumer> _logger;

    public WelcomeMailConsumer(
        IConnection connection,
        IOptions<JsonOptions> jsonOptions,
        ILogger<WelcomeMailConsumer> logger)
    {
        _connection = connection;
        _jsonOptions = jsonOptions.Value.JsonSerializerOptions;
        _logger = logger;
    }

    private static string? ReadHeader(
        BasicDeliverEventArgs message,
        string name)
    {
        if (message.BasicProperties.Headers is not { } headers
            || !headers.TryGetValue(name, out var value))
        {
            return null;
        }

        return value is byte[] bytes
            ? Encoding.UTF8.GetString(bytes)
            : value as string;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        await using var channel =
            await _connection.CreateChannelAsync();

        // Declaring the queue here as well means the two services can start
        // in any order.
        await channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, message) =>
        {
            ActivityContext.TryParse(
                ReadHeader(message, "traceparent"),
                traceState: null,
                isRemote: true,
                out var parentContext);

            using var activity = MessagingSource.StartActivity(
                "Welcome mail",
                ActivityKind.Consumer,
                parentContext);

            try
            {
                var subscriber =
                    JsonSerializer.Deserialize<SubscriberCreated>(
                        message.Body.Span, _jsonOptions)
                    ?? throw new JsonException("Subscriber message was null.");

                _logger.LogInformation(
                    "Simulated welcome mail to subscriber {SubscriberId}",
                    subscriber.Id);

                await channel.BasicAckAsync(
                    deliveryTag: message.DeliveryTag,
                    multiple: false);
            }
            catch (Exception exception)
            {
                activity?.SetStatus(
                    ActivityStatusCode.Error,
                    exception.Message);

                // A broken welcome message is discarded instead of stopping
                // the service, so newsletters keep being sent.
                _logger.LogError(
                    exception,
                    "Welcome mail failed; the message was discarded.");

                await channel.BasicNackAsync(
                    deliveryTag: message.DeliveryTag,
                    multiple: false,
                    requeue: false);
            }
        };

        await channel.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false,
            consumer: consumer);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
    }
}
