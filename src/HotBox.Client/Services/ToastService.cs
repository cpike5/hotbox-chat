namespace HotBox.Client.Services;

public enum ToastType
{
    Success,
    Error,
    Warning,
    Info
}

public record ToastItem(Guid Id, string Message, ToastType Type, DateTime CreatedAt);

public class ToastService : IDisposable
{
    private readonly List<ToastItem> _toasts = new();
    private readonly Dictionary<Guid, CancellationTokenSource> _autoDismissTasks = new();
    private const int DefaultDurationMs = 5000;
    private bool _disposed;

    public IReadOnlyList<ToastItem> Toasts => _toasts;

    public event Action? OnChange;

    public void ShowSuccess(string message, int? durationMs = null)
        => Show(message, ToastType.Success, durationMs);

    public void ShowError(string message, int? durationMs = null)
        => Show(message, ToastType.Error, durationMs);

    public void ShowWarning(string message, int? durationMs = null)
        => Show(message, ToastType.Warning, durationMs);

    public void ShowInfo(string message, int? durationMs = null)
        => Show(message, ToastType.Info, durationMs);

    public void RemoveToast(Guid id)
    {
        var removed = _toasts.RemoveAll(t => t.Id == id);

        if (_autoDismissTasks.TryGetValue(id, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
            _autoDismissTasks.Remove(id);
        }

        if (removed > 0)
        {
            OnChange?.Invoke();
        }
    }

    private void Show(string message, ToastType type, int? durationMs)
    {
        var toast = new ToastItem(Guid.NewGuid(), message, type, DateTime.UtcNow);
        _toasts.Add(toast);
        OnChange?.Invoke();

        var cts = new CancellationTokenSource();
        _autoDismissTasks[toast.Id] = cts;
        _ = AutoDismissAsync(toast.Id, durationMs ?? DefaultDurationMs, cts.Token);
    }

    private async Task AutoDismissAsync(Guid id, int durationMs, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(durationMs, cancellationToken);
            RemoveToast(id);
        }
        catch (TaskCanceledException)
        {
            // Toast was manually dismissed before auto-dismiss fired; nothing to do.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var cts in _autoDismissTasks.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }

        _autoDismissTasks.Clear();
    }
}
