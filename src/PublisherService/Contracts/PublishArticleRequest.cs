using System.ComponentModel.DataAnnotations;
using PublisherService.Models;

namespace PublisherService.Contracts;

public class PublishArticleRequest
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    [Required]
    public ArticleScope? Scope { get; set; }
}