namespace HotBox.Core.Interfaces;

public interface INotificationService
{
    /// <summary>
    /// Checks the message content for @mentions and sends notifications
    /// to mentioned users who are not the sender.
    /// </summary>
    Task ProcessMessageNotificationsAsync(
        Guid senderId,
        string senderDisplayName,
        Guid channelId,
        string channelName,
        string messageContent);
}
