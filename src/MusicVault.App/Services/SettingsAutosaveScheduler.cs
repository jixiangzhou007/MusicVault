namespace MusicVault.App.Services;

public enum SettingsAutosaveKind
{
    Instant,
    Debounced
}

/// <summary>设置偏好自动保存：即时 / 400ms 防抖 / 强制冲刷。可注入 delay 便于单测。</summary>
public sealed class SettingsAutosaveScheduler
{
    public const int DebounceMilliseconds = 400;

    private readonly Func<CancellationToken, Task> _persist;
    private readonly Func<int, CancellationToken, Task> _delay;
    private readonly object _gate = new();
    private CancellationTokenSource? _debounceCts;
    private bool _dirty;
    private Task _chain = Task.CompletedTask;

    public SettingsAutosaveScheduler(
        Func<CancellationToken, Task> persist,
        Func<int, CancellationToken, Task>? delay = null)
    {
        _persist = persist ?? throw new ArgumentNullException(nameof(persist));
        _delay = delay ?? ((ms, ct) => Task.Delay(ms, ct));
    }

    public Task ScheduleAsync(SettingsAutosaveKind kind)
    {
        if (kind == SettingsAutosaveKind.Instant)
        {
            CancelDebounce();
            _dirty = true;
            return EnqueuePersistAsync();
        }

        _dirty = true;
        CancellationTokenSource cts;
        lock (_gate)
        {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            cts = new CancellationTokenSource();
            _debounceCts = cts;
        }

        return DebounceThenPersistAsync(cts);
    }

    public Task FlushAsync()
    {
        CancelDebounce();
        if (!_dirty)
            return Task.CompletedTask;
        return EnqueuePersistAsync();
    }

    private async Task DebounceThenPersistAsync(CancellationTokenSource cts)
    {
        try
        {
            await _delay(DebounceMilliseconds, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        lock (_gate)
        {
            if (_debounceCts != cts)
                return;
            _debounceCts = null;
        }

        await EnqueuePersistAsync().ConfigureAwait(false);
    }

    private Task EnqueuePersistAsync()
    {
        lock (_gate)
        {
            _chain = ContinuePersistAsync(_chain);
            return _chain;
        }
    }

    private async Task ContinuePersistAsync(Task previous)
    {
        try
        {
            await previous.ConfigureAwait(false);
        }
        catch
        {
            // Prior persist errors are already surfaced by the persist delegate.
        }

        if (!_dirty)
            return;

        _dirty = false;
        try
        {
            await _persist(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            _dirty = true;
            throw;
        }
    }

    private void CancelDebounce()
    {
        lock (_gate)
        {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = null;
        }
    }
}
