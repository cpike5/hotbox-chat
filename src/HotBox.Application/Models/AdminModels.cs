using System.ComponentModel.DataAnnotations;
using HotBox.Core.Enums;

namespace HotBox.Application.Models;

public record ServerSettingsResponse(
    Guid Id,
    string ServerName,
    RegistrationMode RegistrationMode);

public record UpdateServerSettingsRequest(
    [Required] string ServerName,
    RegistrationMode RegistrationMode);

public record ChangeUserRoleRequest(
    [Required] string Role);

public record BanUserRequest(
    string? Reason);
