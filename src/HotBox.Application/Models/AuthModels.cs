using System.ComponentModel.DataAnnotations;

namespace HotBox.Application.Models;

public record LoginRequest(
    [Required] string Email,
    [Required] string Password);

public record RegisterRequest(
    [Required] string Email,
    [Required] string Password,
    [Required] string DisplayName,
    string? InviteCode);

public record AuthResponse(
    string AccessToken,
    DateTime ExpiresAt,
    UserProfileResponse User);
