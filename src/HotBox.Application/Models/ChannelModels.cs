using System.ComponentModel.DataAnnotations;
using HotBox.Core.Enums;

namespace HotBox.Application.Models;

public record ChannelResponse(
    Guid Id,
    string Name,
    string? Description,
    ChannelType Type,
    int SortOrder,
    DateTime CreatedAt,
    Guid CreatedById);

public record CreateChannelRequest(
    [Required] string Name,
    string? Description,
    ChannelType Type = ChannelType.Text);

public record UpdateChannelRequest(
    [Required] string Name,
    string? Description);
