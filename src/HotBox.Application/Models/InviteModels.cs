namespace HotBox.Application.Models;

public record InviteResponse(
    Guid Id,
    string Code,
    Guid CreatedById,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    int? MaxUses,
    int UseCount,
    bool IsRevoked);

public record CreateInviteRequest(
    DateTime? ExpiresAt,
    int? MaxUses);

public record ValidateInviteResponse(
    bool IsValid,
    string Code);
