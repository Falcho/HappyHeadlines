using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NewsletterService.Contracts;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace NewsletterService.Services;

public class SubscriberClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<SubscriberClient> _logger;

    public SubscriberClient(
        IHttpClientFactory httpClientFactory,
        IOptions<JsonOptions> jsonOptions,
        ILogger<SubscriberClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _jsonOptions = jsonOptions.Value.JsonSerializerOptions;
        _logger = logger;
    }

    // Returns null when SubscriberService cannot deliver its subscribers:
    // it is down, too slow, the circuit is open, or its release toggle is off.
    // Callers then skip sending instead of failing.
    public async Task<List<Subscriber>?> GetSubscribersAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("SubscriberService");

            using var response = await client.GetAsync(
                "/subscribers",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "SubscriberService answered {StatusCode}",
                    (int)response.StatusCode);

                return null;
            }

            return await response.Content.ReadFromJsonAsync<List<Subscriber>>(
                _jsonOptions,
                cancellationToken);
        }
        catch (Exception exception) when (exception
            is HttpRequestException
            or BrokenCircuitException
            or TimeoutRejectedException
            or JsonException)
        {
            _logger.LogWarning(
                "SubscriberService is unavailable: {Reason}",
                exception.Message);

            return null;
        }
    }
}
