using System.ComponentModel.DataAnnotations;

namespace HotBox.Application.Models;

public record CreateAgentRequest(
    [Required] string DisplayName,
    string? Bio);

public record AgentResponse(
    Guid Id,
    string DisplayName,
    string? Bio,
    bool IsAgent,
    DateTime CreatedAt);
