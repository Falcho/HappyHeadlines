using System.ComponentModel.DataAnnotations;

namespace DraftService.Contracts;

public class SaveDraftRequest
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = true)]
    public string Content { get; set; } = string.Empty;
}