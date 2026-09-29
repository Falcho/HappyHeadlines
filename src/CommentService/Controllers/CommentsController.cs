using System.Text.Json;
using CommentService.Contracts;
using CommentService.Data;
using CommentService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace CommentService.Controllers;

[ApiController]
[Route("comments")]
public class CommentsController : ControllerBase
{
    private readonly CommentDbContext _database;
    private readonly IHttpClientFactory _httpClientFactory;

    public CommentsController(
        CommentDbContext database,
        IHttpClientFactory httpClientFactory)
    {
        _database = database;
        _httpClientFactory = httpClientFactory;
    }

    [HttpPost]
    public async Task<ActionResult<Comment>> Create(
        CreateCommentRequest request)
    {
        if (request.ArticleId == Guid.Empty)
        {
            return BadRequest("ArticleId must not be an empty GUID.");
        }

        var client = _httpClientFactory.CreateClient("ProfanityService");

        try
        {
            using var response = await client.PostAsJsonAsync(
                "/profanity/check",
                new { text = request.Content });

            response.EnsureSuccessStatusCode();

            var result = await response.Content
                .ReadFromJsonAsync<ProfanityCheckResponse>();

            if (result?.ContainsProfanity is null)
            {
                return StatusCode(503, "Profanity checking is unavailable.");
            }

            if (result.ContainsProfanity.Value)
            {
                return BadRequest("The comment contains prohibited language.");
            }
        }
        catch (BrokenCircuitException)
        {
            return StatusCode(
                503,
                "Profanity checking is temporarily paused. Try again shortly.");
        }
        catch (TimeoutRejectedException)
        {
            return StatusCode(503, "Profanity checking timed out.");
        }
        catch (HttpRequestException)
        {
            return StatusCode(503, "Profanity checking is unavailable.");
        }
        catch (TaskCanceledException)
        {
            return StatusCode(503, "Profanity checking timed out.");
        }
        catch (JsonException)
        {
            return StatusCode(503, "Profanity checking returned an invalid response.");
        }

        var comment = new Comment
        {
            Id = Guid.NewGuid(),
            ArticleId = request.ArticleId!.Value,
            Content = request.Content
        };

        _database.Comments.Add(comment);
        await _database.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, comment);
    }

    [HttpGet("article/{articleId:guid}")]
    public async Task<ActionResult<List<Comment>>> ReadForArticle(
        Guid articleId)
    {
        var comments = await _database.Comments
            .Where(comment => comment.ArticleId == articleId)
            .ToListAsync();

        return Ok(comments);
    }
}