namespace HotBox.Core.Entities;

public class UserChannelRead
{
    public Guid UserId { get; set; }

    public Guid ChannelId { get; set; }

    public Guid? LastReadMessageId { get; set; }

    public DateTime LastReadAtUtc { get; set; }

    public AppUser User { get; set; } = null!;

    public Channel Channel { get; set; } = null!;

    public Message? LastReadMessage { get; set; }
}
