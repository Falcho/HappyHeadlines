using ArticleService.Data;
using ArticleService.Models;
using Microsoft.EntityFrameworkCore;

namespace ArticleService.Caching;

public class ArticleCacheWorker : BackgroundService
{
    private readonly ArticleDbContextFactory _databaseFactory;
    private readonly ArticleCache _cache;
    private readonly ILogger<ArticleCacheWorker> _logger;

    public ArticleCacheWorker(
        ArticleDbContextFactory databaseFactory,
        ArticleCache cache,
        ILogger<ArticleCacheWorker> logger)
    {
        _databaseFactory = databaseFactory;
        _cache = cache;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        try
        {
            do
            {
                try
                {
                    await RefreshAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogError(
                        exception,
                        "Article cache refresh failed.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // The application is shutting down.
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(-14);

        await using var database =
            _databaseFactory.Create(ArticleScope.Global);

        var articles = await database.Articles
            .AsNoTracking()
            .Where(article =>
                article.PublishedAt >= cutoff &&
                article.PublishedAt <= now)
            .ToListAsync(cancellationToken);

        _cache.Replace(articles);

        _logger.LogInformation(
            "Article cache refreshed with {ArticleCount} global articles.",
            articles.Count);
    }
}