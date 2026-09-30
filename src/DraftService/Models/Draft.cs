namespace DraftService.Models;

public class Draft
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
}