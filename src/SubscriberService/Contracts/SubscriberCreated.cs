namespace SubscriberService.Contracts;

public class SubscriberCreated
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public DateTimeOffset SubscribedAt { get; set; }
}
