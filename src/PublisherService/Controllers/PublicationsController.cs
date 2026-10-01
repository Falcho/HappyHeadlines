using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PublisherService.Contracts;
using RabbitMQ.Client;

namespace PublisherService.Controllers;

[ApiController]
[Route("publications")]
public class PublicationsController : ControllerBase
{
    private static readonly ActivitySource MessagingSource =
        new("HappyHeadlines.Messaging");

    private readonly IConnection _connection;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly JsonSerializerOptions _jsonOptions;

    public PublicationsController(
        IConnection connection,
        IHttpClientFactory httpClientFactory,
        IOptions<JsonOptions> jsonOptions)
    {
        _connection = connection;
        _httpClientFactory = httpClientFactory;
        _jsonOptions = jsonOptions.Value.JsonSerializerOptions;
    }

    [HttpPost]
    public async Task<IActionResult> Publish(
        PublishArticleRequest request)
    {
        var client = _httpClientFactory.CreateClient("ProfanityService");

        try
        {
            using var response = await client.PostAsJsonAsync(
                "/profanity/check",
                new { text = $"{request.Title}\n{request.Content}" });

            response.EnsureSuccessStatusCode();

            var result = await response.Content
                .ReadFromJsonAsync<ProfanityCheckResponse>();

            if (result?.ContainsProfanity is not bool containsProfanity)
            {
                return StatusCode(
                    503,
                    "Profanity checking returned an invalid response.");
            }

            if (containsProfanity)
            {
                return BadRequest(
                    "The article contains prohibited language.");
            }
        }
        catch (HttpRequestException)
        {
            return StatusCode(
                503,
                "Profanity checking is unavailable.");
        }
        catch (OperationCanceledException)
        {
            return StatusCode(
                503,
                "Profanity checking timed out.");
        }
        catch (JsonException)
        {
            return StatusCode(
                503,
                "Profanity checking returned an invalid response.");
        }

        using var activity = MessagingSource.StartActivity(
            "Publish article",
            ActivityKind.Producer);

        var article = new ArticlePublished
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Content = request.Content,
            Scope = request.Scope!.Value,
            PublishedAt = DateTimeOffset.UtcNow
        };

        var body = JsonSerializer.SerializeToUtf8Bytes(
            article, _jsonOptions);

        var channelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);

        await using var channel =
            await _connection.CreateChannelAsync(channelOptions);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            Persistent = true,
            MessageId = article.Id.ToString(),
            Headers = new Dictionary<string, object?>()
        };

        var currentActivity = Activity.Current;

        if (currentActivity?.Id is string traceParent)
        {
            properties.Headers["traceparent"] =
                Encoding.UTF8.GetBytes(traceParent);
        }

        if (currentActivity?.TraceStateString is string traceState)
        {
            properties.Headers["tracestate"] =
                Encoding.UTF8.GetBytes(traceState);
        }

        await channel.BasicPublishAsync(
            exchange: "articles.published",
            routingKey: string.Empty,
            mandatory: true,
            basicProperties: properties,
            body: body);

        return Accepted(article);
    }
}