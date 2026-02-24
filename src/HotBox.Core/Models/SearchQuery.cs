using HotBox.Core.Enums;

namespace HotBox.Core.Models;

public class SearchQuery
{
    public string QueryText { get; set; } = string.Empty;

    public Guid? ChannelId { get; set; }

    public Guid? SenderId { get; set; }

    public string? Cursor { get; set; }

    public int Limit { get; set; } = 20;

    public SearchScope Scope { get; set; } = SearchScope.All;

    public Guid? CallerUserId { get; set; }
}
