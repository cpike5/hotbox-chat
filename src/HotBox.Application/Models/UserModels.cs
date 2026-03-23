using HotBox.Core.Enums;

namespace HotBox.Application.Models;

public record UserProfileResponse(
    Guid Id,
    string DisplayName,
    string Email,
    string? AvatarUrl,
    string? Bio,
    string? Pronouns,
    string? CustomStatus,
    UserStatus Status,
    bool IsAgent,
    IList<string> Roles,
    DateTime CreatedAt,
    DateTime LastSeenAt);

public record UpdateProfileRequest(
    string? DisplayName,
    string? AvatarUrl,
    string? Bio,
    string? Pronouns,
    string? CustomStatus);

public record OnlineUserInfo(
    Guid UserId,
    string DisplayName,
    UserStatus Status,
    bool IsAgent);
