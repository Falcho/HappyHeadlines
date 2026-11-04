using System.ComponentModel.DataAnnotations;

namespace SubscriberService.Contracts;

public class SubscribeRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}