using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace HotBox.Client.Services;

public class BrowserNotificationService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<BrowserNotificationService> _logger;
    private bool _permissionRequested;

    public BrowserNotificationService(IJSRuntime jsRuntime, ILogger<BrowserNotificationService> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    /// <summary>
    /// Requests notification permission if not already requested.
    /// Returns the permission state: "granted", "denied", or "default".
    /// </summary>
    public async Task<string> RequestPermissionAsync()
    {
        if (_permissionRequested)
        {
            return "already-requested";
        }

        try
        {
            _permissionRequested = true;
            var result = await _jsRuntime.InvokeAsync<string>(
                "hotboxNotifications.requestNotificationPermission");
            _logger.LogInformation("Notification permission result: {PermissionState}", result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to request notification permission");
            return "denied";
        }
    }

    /// <summary>
    /// Shows a desktop notification if the browser tab is not focused and permission is granted.
    /// Requests permission on first invocation.
    /// </summary>
    public async Task ShowNotificationIfHiddenAsync(string senderName, string messagePreview)
    {
        try
        {
            // Request permission on first notification attempt (not on page load)
            if (!_permissionRequested)
            {
                var permission = await RequestPermissionAsync();
                if (permission == "denied")
                {
                    return;
                }
            }

            var isVisible = await _jsRuntime.InvokeAsync<bool>(
                "hotboxNotifications.isDocumentVisible");

            if (isVisible)
            {
                return;
            }

            var title = $"Message from {senderName}";
            var body = messagePreview.Length > 100
                ? messagePreview[..100]
                : messagePreview;

            await _jsRuntime.InvokeVoidAsync(
                "hotboxNotifications.showNotification", title, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show notification for sender {SenderName}", senderName);
        }
    }
}
