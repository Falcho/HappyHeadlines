using CommentService.Caching;
using Microsoft.AspNetCore.Mvc;

namespace CommentService.Controllers;

[ApiController]
[Route("cache")]
public class CacheController : ControllerBase
{
    private readonly CommentCache _cache;

    public CacheController(CommentCache cache)
    {
        _cache = cache;
    }

    [HttpGet("statistics")]
    public IActionResult GetStatistics()
    {
        var statistics = _cache.GetStatistics();

        return Ok(new
        {
            hits = statistics.Hits,
            misses = statistics.Misses,
            hitRatio = statistics.HitRatio,
            articleCount = statistics.ArticleCount
        });
    }
}