namespace HotBox.Client.Models;

public class OnlineUserInfoModel
{
    public Guid UserId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
}
