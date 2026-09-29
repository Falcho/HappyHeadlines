namespace CommentService.Models;

public class Comment
{
    public Guid Id { get; set; }
    public Guid ArticleId { get; set; }
    public string Content { get; set; } = string.Empty;
}