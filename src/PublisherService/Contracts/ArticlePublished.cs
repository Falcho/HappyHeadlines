using PublisherService.Models;

namespace PublisherService.Contracts;

public class ArticlePublished
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public ArticleScope Scope { get; set; }

    public DateTimeOffset PublishedAt { get; set; }
}