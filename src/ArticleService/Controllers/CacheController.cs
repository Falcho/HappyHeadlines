using ArticleService.Caching;
using Microsoft.AspNetCore.Mvc;

namespace ArticleService.Controllers;

[ApiController]
[Route("cache")]
public class CacheController : ControllerBase
{
    private readonly ArticleCache _cache;

    public CacheController(ArticleCache cache)
    {
        _cache = cache;
    }

    [HttpGet("statistics")]
    public IActionResult GetStatistics()
    {
        var statistics = _cache.GetStatistics();

        return Ok(new
        {
            instance = Environment.GetEnvironmentVariable("INSTANCE_NAME")
                ?? "local",
            hits = statistics.Hits,
            misses = statistics.Misses,
            hitRatio = statistics.HitRatio
        });
    }
}