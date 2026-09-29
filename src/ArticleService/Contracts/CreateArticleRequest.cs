using System.ComponentModel.DataAnnotations;
using ArticleService.Models;

namespace ArticleService.Contracts;

public class CreateArticleRequest
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    [Required]
    public ArticleScope? Scope { get; set; }
}