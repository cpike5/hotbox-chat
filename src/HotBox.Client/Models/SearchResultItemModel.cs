namespace HotBox.Client.Models;

public class SearchResultItemModel
{
    public Guid MessageId { get; set; }
    public string Snippet { get; set; } = string.Empty;
    public Guid ChannelId { get; set; }
    public string ChannelName { get; set; } = string.Empty;
    public Guid AuthorId { get; set; }
    public string AuthorDisplayName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public double RelevanceScore { get; set; }
    public bool IsDirectMessage { get; set; }
    public Guid? OtherParticipantId { get; set; }
    public string? OtherParticipantDisplayName { get; set; }
}
