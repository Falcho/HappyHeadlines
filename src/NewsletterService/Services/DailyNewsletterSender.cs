using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NewsletterService.Contracts;

namespace NewsletterService.Services;

public class DailyNewsletterSender
{
    private static readonly ActivitySource Source =
        new("HappyHeadlines.Messaging");

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<DailyNewsletterSender> _logger;
    private readonly SubscriberClient _subscriberClient;

    public DailyNewsletterSender(
        IHttpClientFactory httpClientFactory,
        IOptions<JsonOptions> jsonOptions,
        ILogger<DailyNewsletterSender> logger,
        SubscriberClient subscriberClient)
    {
        _httpClientFactory = httpClientFactory;
        _jsonOptions = jsonOptions.Value.JsonSerializerOptions;
        _logger = logger;
        _subscriberClient = subscriberClient;
    }

    public async Task<(int ArticleCount, int? SubscriberCount)> SendAsync(
        CancellationToken cancellationToken = default)
    {
        using var activity = Source.StartActivity("Daily newsletter");

        try
        {
            var client = _httpClientFactory.CreateClient("ArticleService");

            var articles = await client.GetFromJsonAsync<List<ArticlePublished>>(
                "/articles/recent",
                _jsonOptions,
                cancellationToken)
                ?? throw new JsonException("Article list was null.");

            var subscribers =
                await _subscriberClient.GetSubscribersAsync(cancellationToken);

            if (subscribers is null)
            {
                _logger.LogWarning(
                    "Daily newsletter skipped: subscribers are unavailable.");

                return (articles.Count, null);
            }

            var titles = string.Join(
                "; ",
                articles.Select(article => article.Title));

            _logger.LogInformation(
                "Simulated daily newsletter with {ArticleCount} articles to {SubscriberCount} subscribers: {Titles}",
                articles.Count,
                subscribers.Count,
                titles);

            return (articles.Count, subscribers.Count);
        }
        catch (Exception exception)
        {
            activity?.SetStatus(
                ActivityStatusCode.Error,
                exception.Message);

            throw;
        }
    }
}