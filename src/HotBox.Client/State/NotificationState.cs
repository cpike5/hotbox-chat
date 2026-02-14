using HotBox.Client.Models;
using HotBox.Client.Services;
using HotBox.Core.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace HotBox.Client.State;

public class NotificationState : IDisposable
{
    private readonly ApiClient _api;
    private readonly ChatHubService _chatHub;
    private readonly ToastService _toastService;
    private readonly NavigationManager _navigation;
    private readonly ILogger<NotificationState> _logger;

    private readonly List<NotificationResponseModel> _notifications = new();
    private int _unreadCount;
    private bool _initialized;

    public event Action? OnChange;

    public IReadOnlyList<NotificationResponseModel> Notifications => _notifications;
    public int UnreadCount => _unreadCount;

    public NotificationState(
        ApiClient api,
        ChatHubService chatHub,
        ToastService toastService,
        NavigationManager navigation,
        ILogger<NotificationState> logger)
    {
        _api = api;
        _chatHub = chatHub;
        _toastService = toastService;
        _navigation = navigation;
        _logger = logger;

        _chatHub.OnNotificationReceived += HandleNotificationReceived;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        try
        {
            _unreadCount = await _api.GetNotificationUnreadCountAsync();
            _initialized = true;
            NotifyStateChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize notification state");
        }
    }

    public async Task LoadHistoryAsync(DateTime? before = null)
    {
        try
        {
            var notifications = await _api.GetNotificationsAsync(before);
            if (notifications is not null)
            {
                if (before.HasValue)
                {
                    _notifications.AddRange(notifications);
                }
                else
                {
                    _notifications.Clear();
                    _notifications.AddRange(notifications);
                }
                NotifyStateChanged();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load notification history");
        }
    }

    public async Task MarkAllAsReadAsync()
    {
        try
        {
            await _api.MarkAllNotificationsReadAsync();
            _unreadCount = 0;

            foreach (var n in _notifications)
            {
                n.ReadAtUtc ??= DateTime.UtcNow;
            }

            NotifyStateChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark all notifications as read");
        }
    }

    private void HandleNotificationReceived(NotificationResponseModel notification)
    {
        _notifications.Insert(0, notification);
        _unreadCount++;

        // Build toast message based on type
        var message = notification.Type switch
        {
            NotificationType.Mention => $"{notification.SenderDisplayName} mentioned you in #{notification.SourceName}",
            NotificationType.DirectMessage => $"{notification.SenderDisplayName} sent you a message",
            _ => $"New notification from {notification.SenderDisplayName}"
        };

        // Build navigation URL based on source type
        var navigateUrl = notification.SourceType switch
        {
            NotificationSourceType.Channel => $"/channels/{notification.SourceId}",
            NotificationSourceType.DirectMessage => $"/dm/{notification.SourceId}",
            _ => null
        };

        _toastService.ShowInfo(message, navigateUrl);

        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();

    public void Dispose()
    {
        _chatHub.OnNotificationReceived -= HandleNotificationReceived;
    }
}
