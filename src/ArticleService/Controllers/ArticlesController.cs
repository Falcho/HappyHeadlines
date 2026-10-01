using ArticleService.Contracts;
using ArticleService.Data;
using ArticleService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArticleService.Controllers;

[ApiController]
[Route("articles")]
public class ArticlesController : ControllerBase
{
    private readonly ArticleDbContextFactory _databaseFactory;

    public ArticlesController(ArticleDbContextFactory databaseFactory)
    {
        _databaseFactory = databaseFactory;
    }

    [HttpPost]
    public async Task<ActionResult<Article>> Create(
        CreateArticleRequest request)
    {
        var article = new Article
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Content = request.Content,
            Scope = request.Scope!.Value,
            PublishedAt = DateTime.UtcNow
        };

        await using var database =
            _databaseFactory.Create(article.Scope);

        database.Articles.Add(article);
        await database.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, article);
    }

    [HttpGet("recent")]
    public async Task<ActionResult<List<Article>>> ReadRecent()
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(-1);
        var articles = new List<Article>();

        foreach (var scope in Enum.GetValues<ArticleScope>())
        {
            await using var database = _databaseFactory.Create(scope);

            var recentArticles = await database.Articles
                .AsNoTracking()
                .Where(article =>
                    article.PublishedAt >= cutoff &&
                    article.PublishedAt <= now)
                .ToListAsync();

            articles.AddRange(recentArticles);
        }

        return Ok(articles
            .OrderByDescending(article => article.PublishedAt)
            .ToList());
    }

    [HttpGet("{scope}/{id:guid}")]
    public async Task<ActionResult<Article>> Read(
        ArticleScope scope,
        Guid id)
    {
        if (!Enum.IsDefined(scope))
        {
            return BadRequest("Invalid article scope.");
        }

        await using var database = _databaseFactory.Create(scope);

        var article = await database.Articles.FindAsync(id);

        if (article is null)
        {
            return NotFound();
        }

        return Ok(article);
    }

    [HttpPut("{scope}/{id:guid}")]
    public async Task<ActionResult<Article>> Update(
        ArticleScope scope,
        Guid id,
        UpdateArticleRequest request)
    {
        if (!Enum.IsDefined(scope))
        {
            return BadRequest("Invalid article scope.");
        }

        await using var database = _databaseFactory.Create(scope);

        var article = await database.Articles.FindAsync(id);

        if (article is null)
        {
            return NotFound();
        }

        article.Title = request.Title;
        article.Content = request.Content;

        await database.SaveChangesAsync();

        return Ok(article);
    }

    [HttpDelete("{scope}/{id:guid}")]
    public async Task<IActionResult> Delete(
        ArticleScope scope,
        Guid id)
    {
        if (!Enum.IsDefined(scope))
        {
            return BadRequest("Invalid article scope.");
        }

        await using var database = _databaseFactory.Create(scope);

        var article = await database.Articles.FindAsync(id);

        if (article is null)
        {
            return NotFound();
        }

        database.Articles.Remove(article);
        await database.SaveChangesAsync();

        return NoContent();
    }
}