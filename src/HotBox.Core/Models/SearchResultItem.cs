namespace HotBox.Core.Models;

public class SearchResultItem
{
    public Guid MessageId { get; set; }

    public string Snippet { get; set; } = string.Empty;

    public Guid ChannelId { get; set; }

    public string ChannelName { get; set; } = string.Empty;

    public Guid AuthorId { get; set; }

    public string AuthorDisplayName { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public double RelevanceScore { get; set; }
}
