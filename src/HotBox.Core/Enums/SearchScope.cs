using System.Text.Json.Serialization;

namespace HotBox.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SearchScope
{
    All,
    Channels,
    DirectMessages
}
