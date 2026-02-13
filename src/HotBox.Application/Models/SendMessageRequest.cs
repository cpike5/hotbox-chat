using System.ComponentModel.DataAnnotations;

namespace HotBox.Application.Models;

public class SendMessageRequest
{
    [Required]
    [MaxLength(4000)]
    public string Content { get; set; } = string.Empty;
}
