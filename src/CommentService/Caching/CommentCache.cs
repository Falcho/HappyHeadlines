using CommentService.Models;

namespace CommentService.Caching;

public class CommentCache
{
    private const int Capacity = 30;

    private readonly object _lock = new();
    private readonly Dictionary<Guid, List<Comment>> _comments = new();
    private readonly LinkedList<Guid> _accessOrder = new();
    private long _hits;
    private long _misses;

    public List<Comment>? Get(Guid articleId)
    {
        lock (_lock)
        {
            if (!_comments.TryGetValue(articleId, out var comments))
            {
                _misses++;
                return null;
            }

            _hits++;

            _accessOrder.Remove(articleId);
            _accessOrder.AddFirst(articleId);

            return comments;
        }
    }

    public (long Hits, long Misses, double? HitRatio, int ArticleCount)
    GetStatistics()
    {
        lock (_lock)
        {
            var total = _hits + _misses;

            double? hitRatio = total == 0
                ? null
                : (double)_hits / total * 100;

            return (_hits, _misses, hitRatio, _comments.Count);
        }
    }

    public void Set(Guid articleId, List<Comment> comments)
    {
        lock (_lock)
        {
            _comments[articleId] = comments;

            _accessOrder.Remove(articleId);
            _accessOrder.AddFirst(articleId);

            if (_comments.Count > Capacity)
            {
                var oldestArticleId = _accessOrder.Last!.Value;

                _accessOrder.RemoveLast();
                _comments.Remove(oldestArticleId);
            }
        }
    }

    public void Remove(Guid articleId)
    {
        lock (_lock)
        {
            _comments.Remove(articleId);
            _accessOrder.Remove(articleId);
        }
    }
}