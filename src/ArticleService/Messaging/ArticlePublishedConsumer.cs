using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ArticleService.Contracts;
using ArticleService.Data;
using ArticleService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ArticleService.Messaging;

public class ArticlePublishedConsumer : BackgroundService
{
    private static readonly ActivitySource MessagingSource =
        new("HappyHeadlines.Messaging");

    private readonly IConnection _connection;
    private readonly ArticleDbContextFactory _databaseFactory;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<ArticlePublishedConsumer> _logger;
    private readonly IHostApplicationLifetime _lifetime;

    public ArticlePublishedConsumer(
        IConnection connection,
        ArticleDbContextFactory databaseFactory,
        IOptions<JsonOptions> jsonOptions,
        ILogger<ArticlePublishedConsumer> logger,
        IHostApplicationLifetime lifetime)
    {
        _connection = connection;
        _databaseFactory = databaseFactory;
        _jsonOptions = jsonOptions.Value.JsonSerializerOptions;
        _logger = logger;
        _lifetime = lifetime;
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

        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, message) =>
        {
            var traceParent = ReadHeader(message, "traceparent");
            var traceState = ReadHeader(message, "tracestate");

            ActivityContext.TryParse(
                traceParent,
                traceState,
                isRemote: true,
                out var parentContext);

            using var activity = MessagingSource.StartActivity(
                "Store article",
                ActivityKind.Consumer,
                parentContext);

            try
            {
                var published =
                    JsonSerializer.Deserialize<ArticlePublished>(
                        message.Body.Span, _jsonOptions)
                    ?? throw new JsonException("Article message was null.");

                await using var database =
                    _databaseFactory.Create(published.Scope);

                var existing =
                    await database.Articles.FindAsync(published.Id);

                if (existing is null)
                {
                    database.Articles.Add(new Article
                    {
                        Id = published.Id,
                        Title = published.Title,
                        Content = published.Content,
                        Scope = published.Scope,
                        PublishedAt = published.PublishedAt.UtcDateTime
                    });

                    await database.SaveChangesAsync();
                }

                await channel.BasicAckAsync(
                    deliveryTag: message.DeliveryTag,
                    multiple: false);

                _logger.LogInformation(
                    "Processed published article {ArticleId}",
                    published.Id);
            }
            catch (Exception exception)
            {
                activity?.SetStatus(
                    ActivityStatusCode.Error,
                    exception.Message);

                _logger.LogError(
                    exception,
                    "Article processing failed; stopping this instance.");

                _lifetime.StopApplication();
            }
        };

        await channel.BasicConsumeAsync(
            queue: "articles.store",
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