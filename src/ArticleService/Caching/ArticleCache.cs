using ArticleService.Models;

namespace ArticleService.Caching;

public class ArticleCache
{
    private readonly object _lock = new();
    private Dictionary<Guid, Article> _articles = new();
    private long _hits;
    private long _misses;

    public Article? Get(Guid id)
    {
        lock (_lock)
        {
            if (_articles.TryGetValue(id, out var article))
            {
                _hits++;
                return article;
            }

            _misses++;
            return null;
        }
    }

    public (long Hits, long Misses, double? HitRatio) GetStatistics()
    {
        lock (_lock)
        {
            var total = _hits + _misses;

            double? hitRatio = total == 0
                ? null
                : (double)_hits / total * 100;

            return (_hits, _misses, hitRatio);
        }
    }

    public void Replace(IEnumerable<Article> articles)
    {
        var updatedArticles = articles.ToDictionary(article => article.Id);

        lock (_lock)
        {
            _articles = updatedArticles;
        }
    }
}