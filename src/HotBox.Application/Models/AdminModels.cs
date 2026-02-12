using System.ComponentModel.DataAnnotations;
using HotBox.Core.Enums;

namespace HotBox.Application.Models;

// --- Server Settings ---

public class AdminSettingsResponse
{
    public string ServerName { get; set; } = string.Empty;

    public RegistrationMode RegistrationMode { get; set; }
}

public class UpdateSettingsRequest
{
    [Required]
    [MaxLength(100)]
    public string ServerName { get; set; } = string.Empty;

    [Required]
    public RegistrationMode RegistrationMode { get; set; }
}

// --- User Management ---

public class AdminUserResponse
{
    public Guid Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime LastSeenUtc { get; set; }

    public bool IsAgent { get; set; }
}

public class CreateUserRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = "Member";
}

public class ChangeRoleRequest
{
    [Required]
    public string Role { get; set; } = string.Empty;
}

// --- Invite Management ---

public class AdminInviteResponse
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public Guid CreatedByUserId { get; set; }

    public string? CreatedByDisplayName { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ExpiresAtUtc { get; set; }

    public int? MaxUses { get; set; }

    public int UseCount { get; set; }

    public bool IsRevoked { get; set; }
}

public class GenerateInviteRequest
{
    public DateTime? ExpiresAtUtc { get; set; }

    public int? MaxUses { get; set; }
}

// --- API Key Management ---

public class CreateApiKeyRequest
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}

public class CreateApiKeyResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}

public class AdminApiKeyResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string MaskedKey { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public string? RevokedReason { get; set; }

    public bool IsActive { get; set; }
}

public class RevokeApiKeyRequest
{
    [MaxLength(500)]
    public string? Reason { get; set; }
}

// --- Channel Management ---

public class ReorderChannelsRequest
{
    [Required]
    public List<Guid> ChannelIds { get; set; } = [];
}
