namespace NewsletterService.Contracts;

public class Subscriber
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public DateTimeOffset SubscribedAt { get; set; }
}
