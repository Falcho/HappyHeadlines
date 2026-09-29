using System.ComponentModel.DataAnnotations;

namespace ProfanityService.Contracts;

public class CheckTextRequest
{
    [Required]
    public string Text { get; set; } = string.Empty;
}