using System.ComponentModel.DataAnnotations;

namespace HotBox.Application.Models;

public class SendDirectMessageRequest
{
    [Required]
    public string Content { get; set; } = string.Empty;
}
