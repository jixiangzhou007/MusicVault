using System.Collections.ObjectModel;
using Avalonia.Threading;

namespace MusicVault.App.Services;

public sealed class MessageService : IMessageService
{
    public static MessageService Instance { get; } = new();

    private const int MaxVisible = 3;
    private static readonly TimeSpan DefaultDuration = TimeSpan.FromMilliseconds(2600);

    private readonly object _gate = new();
    private readonly Dictionary<Guid, CancellationTokenSource> _dismissTimers = new();

    public ObservableCollection<ToastMessage> Messages { get; } = [];

    public void ShowSuccess(string message, TimeSpan? duration = null) =>
        Show(MessageLevel.Success, message, duration);

    public void ShowError(string message, TimeSpan? duration = null) =>
        Show(MessageLevel.Error, message, duration);

    public void ShowInfo(string message, TimeSpan? duration = null) =>
        Show(MessageLevel.Info, message, duration);

    public void ShowWarning(string message, TimeSpan? duration = null) =>
        Show(MessageLevel.Warning, message, duration);

    public void Dismiss(Guid id) => Dispatcher.UIThread.Post(() => RemoveMessage(id));

    private void RemoveMessage(Guid id)
    {
        lock (_gate)
        {
            if (_dismissTimers.Remove(id, out var cts))
                cts.Cancel();
        }

        for (var i = Messages.Count - 1; i >= 0; i--)
        {
            if (Messages[i].Id == id)
            {
                Messages.RemoveAt(i);
                break;
            }
        }
    }

    public void DismissAll()
    {
        Dispatcher.UIThread.Post(() =>
        {
            lock (_gate)
            {
                foreach (var cts in _dismissTimers.Values)
                    cts.Cancel();
                _dismissTimers.Clear();
            }

            Messages.Clear();
        });
    }

    private void Show(MessageLevel level, string message, TimeSpan? duration)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        message = message.Trim();
        var toast = new ToastMessage(Guid.NewGuid(), level, message, DateTimeOffset.Now);
        var delay = duration ?? DefaultDuration;

        Dispatcher.UIThread.Post(() =>
        {
            while (Messages.Count >= MaxVisible)
                RemoveMessage(Messages[0].Id);

            Messages.Add(toast);
            ScheduleDismiss(toast.Id, delay);
        });
    }

    private void ScheduleDismiss(Guid id, TimeSpan delay)
    {
        var cts = new CancellationTokenSource();
        lock (_gate)
            _dismissTimers[id] = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, cts.Token).ConfigureAwait(false);
                if (!cts.IsCancellationRequested)
                    Dismiss(id);
            }
            catch (TaskCanceledException)
            {
            }
        });
    }
}
