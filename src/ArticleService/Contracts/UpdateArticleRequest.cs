using System.ComponentModel.DataAnnotations;

namespace ArticleService.Contracts;

public class UpdateArticleRequest
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;
}