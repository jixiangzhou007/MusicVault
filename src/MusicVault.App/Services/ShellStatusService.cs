using System.Collections.ObjectModel;

namespace MusicVault.App.Services;

public enum ShellStatusLevel
{
    Info,
    Warning,
    Error
}

public sealed record ShellStatusEvent(DateTime At, ShellStatusLevel Level, string Message, string? Source = null)
{
    public string TimestampText => At.ToLocalTime().ToString("HH:mm:ss");
}

/// <summary>全局状态条与「偏好」页最近事件。</summary>
public sealed class ShellStatusService
{
    public static ShellStatusService Instance { get; } = new();

    private const int MaxRecent = 24;
    private readonly object _gate = new();
    private readonly List<ShellStatusEvent> _recent = [];

    public event EventHandler<ShellStatusEvent>? StatusRaised;

    public ObservableCollection<ShellStatusEvent> RecentEvents { get; } = [];

    public void Info(string message, string? source = null) =>
        Raise(ShellStatusLevel.Info, message, source);

    public void Warn(string message, string? source = null) =>
        Raise(ShellStatusLevel.Warning, message, source);

    public void Error(string message, string? source = null, Exception? ex = null)
    {
        var text = ex == null ? message : $"{message} ({ex.Message})";
        Raise(ShellStatusLevel.Error, text, source);
    }

    private void Raise(ShellStatusLevel level, string message, string? source)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        message = message.Trim();
        var evt = new ShellStatusEvent(DateTime.Now, level, message, source);

        lock (_gate)
        {
            _recent.Add(evt);
            while (_recent.Count > MaxRecent)
                _recent.RemoveAt(0);
        }

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            RecentEvents.Add(evt);
            while (RecentEvents.Count > MaxRecent)
                RecentEvents.RemoveAt(0);
            StatusRaised?.Invoke(this, evt);
        });
    }
}
