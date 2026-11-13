using MusicVault.App.Services;

namespace MusicVault.Tests.App;

public sealed class SettingsAutosaveSchedulerTests
{
    [Fact]
    public async Task Instant_invokes_persist_once()
    {
        var calls = 0;
        var delays = new List<int>();
        var s = new SettingsAutosaveScheduler(
            persist: _ =>
            {
                calls++;
                return Task.CompletedTask;
            },
            delay: (ms, _) =>
            {
                delays.Add(ms);
                return Task.CompletedTask;
            });

        await s.ScheduleAsync(SettingsAutosaveKind.Instant);

        Assert.Equal(1, calls);
        Assert.Empty(delays);
    }

    [Fact]
    public async Task Debounced_waits_400ms_then_persists_once_for_burst()
    {
        var calls = 0;
        var s = new SettingsAutosaveScheduler(
            persist: _ =>
            {
                calls++;
                return Task.CompletedTask;
            },
            delay: async (ms, ct) =>
            {
                Assert.Equal(SettingsAutosaveScheduler.DebounceMilliseconds, ms);
                await Task.Yield();
            });

        var first = s.ScheduleAsync(SettingsAutosaveKind.Debounced);
        var second = s.ScheduleAsync(SettingsAutosaveKind.Debounced);
        await Task.WhenAll(first, second);

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Flush_cancels_debounce_and_persists_if_dirty()
    {
        var calls = 0;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var s = new SettingsAutosaveScheduler(
            persist: _ =>
            {
                calls++;
                return Task.CompletedTask;
            },
            delay: async (_, ct) => await gate.Task.WaitAsync(ct));

        var schedule = s.ScheduleAsync(SettingsAutosaveKind.Debounced);
        await s.FlushAsync();
        gate.TrySetCanceled();
        await schedule;

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Instant_cancels_pending_debounce()
    {
        var calls = 0;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var s = new SettingsAutosaveScheduler(
            persist: _ =>
            {
                calls++;
                return Task.CompletedTask;
            },
            delay: async (_, ct) => await gate.Task.WaitAsync(ct));

        var debounced = s.ScheduleAsync(SettingsAutosaveKind.Debounced);
        await s.ScheduleAsync(SettingsAutosaveKind.Instant);
        gate.TrySetCanceled();
        await debounced;

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Flush_noop_when_not_dirty()
    {
        var calls = 0;
        var s = new SettingsAutosaveScheduler(
            persist: _ =>
            {
                calls++;
                return Task.CompletedTask;
            });

        await s.FlushAsync();
        Assert.Equal(0, calls);
    }
}
