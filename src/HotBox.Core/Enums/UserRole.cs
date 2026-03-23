using System.Text.Json.Serialization;

namespace HotBox.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UserRole
{
    Admin,
    Moderator,
    Member
}
