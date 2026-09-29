using System.ComponentModel.DataAnnotations;

namespace CommentService.Contracts;

public class CreateCommentRequest
{
    [Required]
    public Guid? ArticleId { get; set; }

    [Required]
    public string Content { get; set; } = string.Empty;
}