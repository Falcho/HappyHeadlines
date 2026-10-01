using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using NewsletterService.Services;

namespace NewsletterService.Controllers;

[ApiController]
[Route("newsletters")]
public class NewslettersController : ControllerBase
{
    private readonly DailyNewsletterSender _sender;

    public NewslettersController(DailyNewsletterSender sender)
    {
        _sender = sender;
    }

    [HttpPost("daily")]
    public async Task<IActionResult> SendDaily(
        CancellationToken cancellationToken)
    {
        try
        {
            var articleCount = await _sender.SendAsync(cancellationToken);

            return Ok(new
            {
                simulated = true,
                articleCount
            });
        }
        catch (HttpRequestException)
        {
            return StatusCode(503, "ArticleService is unavailable.");
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(503, "Retrieving articles timed out.");
        }
        catch (JsonException)
        {
            return StatusCode(503, "ArticleService returned invalid data.");
        }
    }
}