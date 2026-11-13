# Settings Autosave Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Preference fields in Settings auto-persist (instant toggles/combos; 400ms debounce for text); remove explicit Save UI; flush on section leave / leave Settings / app exit.

**Architecture:** Extract a small testable `SettingsAutosaveScheduler` that schedules Instant vs Debounced calls to a injectible persist delegate. Wire it into `SettingsPageViewModel` by replacing `MarkPendingChanges` with `ScheduleAutosave(...)`. Keep the existing `SaveSettingsAsync` body as the single persist path (quiet success; loud failure). Remove sticky/header save chrome.

**Tech Stack:** .NET 8, CommunityToolkit.Mvvm, xUnit, Avalonia settings VM/view, existing `AppSettingsService.SaveAsync`.

**Spec:** `docs/superpowers/specs/2026-08-20-settings-autosave-design.md`

---

## File map

| File | Role |
|------|------|
| Create `src/MusicVault.App/Services/SettingsAutosaveScheduler.cs` | Instant / Debounced / Flush / serial persist |
| Create `tests/MusicVault.Tests/App/SettingsAutosaveSchedulerTests.cs` | Scheduler unit tests |
| Modify `SettingsPageViewModel.cs` (+ Playback/Download/Online partials) | Schedule instead of pending; quiet persist; flush hooks |
| Modify `SettingsPageView.axaml` | Remove sticky bar + header save |
| Modify `MainWindowViewModel.cs` | Flush when leaving Settings section |
| Modify `App.axaml.cs` | Flush settings autosave before shutdown saves if needed |
| Modify `tests/.../SettingsSectionChromeTests.cs` | Assert no save chrome |
| Modify `docs/PRODUCT_DESIGN.md` | Unfreeze + new save contract |

**Commits:** Only if the user explicitly asks; otherwise skip commit steps.

---

### Task 1: Autosave scheduler (TDD)

**Files:**
- Create: `src/MusicVault.App/Services/SettingsAutosaveScheduler.cs`
- Create: `tests/MusicVault.Tests/App/SettingsAutosaveSchedulerTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
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
            persist: _ => { calls++; return Task.CompletedTask; },
            delay: (ms, ct) => { delays.Add(ms); return Task.CompletedTask; });
        await s.ScheduleAsync(SettingsAutosaveKind.Instant);
        Assert.Equal(1, calls);
        Assert.Empty(delays);
    }

    [Fact]
    public async Task Debounced_waits_400ms_then_persists_once()
    {
        var calls = 0;
        var s = new SettingsAutosaveScheduler(
            persist: _ => { calls++; return Task.CompletedTask; },
            delay: (ms, ct) =>
            {
                Assert.Equal(SettingsAutosaveScheduler.DebounceMilliseconds, ms);
                return Task.CompletedTask;
            });
        await s.ScheduleAsync(SettingsAutosaveKind.Debounced);
        await s.ScheduleAsync(SettingsAutosaveKind.Debounced);
        // After two schedules, only one persist after delay path completes for the last schedule
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Flush_cancels_debounce_and_persists_if_dirty()
    {
        var calls = 0;
        var tcs = new TaskCompletionSource();
        var s = new SettingsAutosaveScheduler(
            persist: _ => { calls++; return Task.CompletedTask; },
            delay: async (ms, ct) =>
            {
                await tcs.Task.WaitAsync(ct);
            });
        var schedule = s.ScheduleAsync(SettingsAutosaveKind.Debounced);
        await s.FlushAsync();
        tcs.TrySetCanceled();
        Assert.Equal(1, calls);
        await schedule; // should complete without throwing unobserved
    }

    [Fact]
    public async Task Instant_cancels_pending_debounce()
    {
        var calls = 0;
        var gate = new TaskCompletionSource();
        var s = new SettingsAutosaveScheduler(
            persist: _ => { calls++; return Task.CompletedTask; },
            delay: async (_, ct) => await gate.Task.WaitAsync(ct));
        var debounced = s.ScheduleAsync(SettingsAutosaveKind.Debounced);
        await s.ScheduleAsync(SettingsAutosaveKind.Instant);
        gate.TrySetCanceled();
        Assert.Equal(1, calls);
        await debounced;
    }
}
```

- [ ] **Step 2: Run tests — expect FAIL (type missing)**

```powershell
dotnet test tests/MusicVault.Tests/MusicVault.Tests.csproj -c Release --filter "FullyQualifiedName~SettingsAutosaveSchedulerTests" -p:WindowsPackageType=None
```

- [ ] **Step 3: Implement scheduler**

```csharp
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
            CancelDebounce_NoLock();
            _dirty = true;
            return EnqueuePersistAsync();
        }

        _dirty = true;
        var cts = new CancellationTokenSource();
        lock (_gate)
        {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = cts;
        }

        return DebounceThenPersistAsync(cts);
    }

    public Task FlushAsync()
    {
        CancelDebounce_NoLock();
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

        if (!cts.IsCancellationRequested)
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
        try { await previous.ConfigureAwait(false); }
        catch { /* prior persist errors surfaced by caller path */ }

        if (!_dirty)
            return;
        _dirty = false;
        await _persist(CancellationToken.None).ConfigureAwait(false);
    }

    private void CancelDebounce_NoLock()
    {
        lock (_gate)
        {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = null;
        }
    }
}
```

Adjust test expectations if serial chain causes Instant-after-Debounced to still be 1 call (desired). Fix Debounced double-schedule test: both complete delay immediately → may get 2 calls unless second cancels first; with cancel-on-reschedule, only last should persist → 1 call when delay is instant Task.CompletedTask.

- [ ] **Step 4: Re-run tests — expect PASS**

---

### Task 2: Wire scheduler into SettingsPageViewModel

**Files:**
- Modify: `src/MusicVault.App/ViewModels/Pages/SettingsPageViewModel.cs`
- Modify: `...SettingsPageViewModel.Playback.cs`
- Modify: `...SettingsPageViewModel.Download.cs`
- Modify: `...SettingsPageViewModel.Online.cs`
- Modify: Gatherer callback if it uses `MarkPendingChanges`

- [ ] **Step 1: Add field + helpers in main VM**

In constructor / fields:

```csharp
private SettingsAutosaveScheduler? _autosave;

// after dependencies ready:
_autosave = new SettingsAutosaveScheduler(PersistPreferencesCoreAsync);

private void ScheduleAutosave(SettingsAutosaveKind kind)
{
    if (_suppressPendingChanges)
        return;
    _ = _autosave!.ScheduleAsync(kind);
}

public Task FlushAutosaveAsync() => _autosave?.FlushAsync() ?? Task.CompletedTask;
```

- [ ] **Step 2: Rename/split SaveSettingsAsync**

- Extract body into `PersistPreferencesCoreAsync(CancellationToken ct)` (same field copies + `_settings.SaveAsync()` + side effects).
- Success: **no** MessageService success toast by default; still set restart hint for LibVLC engine change via StatusMessage / ShellStatus when needed.
- Failure: catch, set StatusMessage + ShellStatus error, rethrow or swallow after UI notify (scheduler should not leave `_dirty` stuck — clear dirty only after successful save; on failure set `_dirty = true` again inside persist wrapper).

Better: persist wrapper in VM:

```csharp
private async Task PersistPreferencesCoreAsync(CancellationToken ct)
{
    try
    {
        await SavePreferencesQuietAsync().ConfigureAwait(true); // former SaveSettings body without toast
        _insights.Track(UsageInsightKeys.SettingsSave);
    }
    catch (Exception ex)
    {
        StatusMessage = ...;
        ShellStatusService.Instance.Error(...);
        throw; // scheduler: on throw, re-mark dirty
    }
}
```

Update scheduler `ContinuePersistAsync`: on persist exception, set `_dirty = true` again.

- [ ] **Step 3: Replace MarkPendingChanges call sites**

| Call site | Kind |
|-----------|------|
| DownloadDirectory, AcoustIdApiKey, MusicApiBaseUrl, NetEaseApiUrl, QqMusicApiUrl, MyFreeMp3QuarkCookies, recognition URL, UpdateFeedUrl, gatherer BaseUrl text | `Debounced` |
| All other former `MarkPendingChanges()` | `Instant` |
| GathererItemViewModel enabled toggle callback | Instant; BaseUrl → Debounced (check GathererItemViewModel) |

Remove `HasPendingChanges` driving UI (can remove property or leave always false). Remove `ShowHeaderSaveButton` / `ShowStickySaveBar` / pending nav dots updates.

- [ ] **Step 4: Flush on `OnSelectedSectionChanged`**

At start of `OnSelectedSectionChanged`:

```csharp
_ = FlushAutosaveAsync();
```

---

### Task 3: Leave Settings + app exit flush

**Files:**
- Modify: `src/MusicVault.App/ViewModels/MainWindowViewModel.cs` (`OnCurrentSectionChanged`)
- Modify: `src/MusicVault.App/App.axaml.cs` if there is exit handler; else flush when MainWindow closing

- [ ] **Step 1: When leaving Settings**

```csharp
partial void OnCurrentSectionChanged(NavigationSection value)
{
    // existing...
    if (_previousWasSettings && value != NavigationSection.Settings)
        _ = Settings.FlushAutosaveAsync();
}
```

Track previous or check before assignment in Navigate path.

- [ ] **Step 2: Exit**

In existing shutdown / MainWindow closing path that already calls `Settings.SaveAsync()`, also:

```csharp
await shell.Settings.FlushAutosaveAsync();
```

so debounced text is not lost. If exit only saves `AppSettingsService.Current` without VM sync, Flush is mandatory (Flush runs VM→settings→Save).

---

### Task 4: Remove save chrome (view + chrome tests)

**Files:**
- Modify: `src/MusicVault.App/Views/Pages/SettingsPageView.axaml`
- Modify: `tests/MusicVault.Tests/App/SettingsSectionChromeTests.cs`

- [ ] **Step 1: Remove** bottom `settings-sticky-save-bar` block and header save `Button` (`settings-save-header-btn`). Keep overview header actions.

- [ ] **Step 2: Chrome asserts**

```csharp
[Fact]
public void Settings_has_no_explicit_save_chrome()
{
    var axaml = ReadSettingsPage();
    Assert.DoesNotContain("settings-sticky-save-bar", axaml);
    Assert.DoesNotContain("settings-save-header-btn", axaml);
    Assert.DoesNotContain("SaveSettingsCommand", axaml);
}
```

- [ ] **Step 3: Run**

```powershell
dotnet test tests/MusicVault.Tests/MusicVault.Tests.csproj -c Release --filter "FullyQualifiedName~SettingsSectionChromeTests|FullyQualifiedName~SettingsAutosaveSchedulerTests" -p:WindowsPackageType=None
```

Expected: all pass.

---

### Task 5: PRODUCT_DESIGN + leftover hints

**Files:**
- Modify: `docs/PRODUCT_DESIGN.md` §6.2, §14.2, §14.3
- Grep/remove unused PersistHint visibility if still shown in axaml
- Optional: keep Appearance instant caption

§6.2 replace with autosave contract (instant / debounce / actions).  
§14.2 remove「全面自动保存」.  
§14.3 mark autosave done; keep「导航收到」as 中期.

---

### Task 6: Publish + manual smoke

```powershell
.\scripts\publish-with-api.ps1 -Restart
```

Manual:
1. Settings → Download → change concurrency → kill app → reopen → value kept  
2. Type API URL quickly → switch to Playback → reopen Online → URL kept  
3. No Save button / sticky bar  
4. Backup still requires button  

---

## Spec coverage check

| Spec § | Task |
|--------|------|
| Instant / Debounced / Actions | 1–2 |
| Path 乙 single persist | 2 |
| Remove save UI | 4 |
| Flush lifecycle | 2–3 |
| PRODUCT_DESIGN | 5 |
| Acceptance | 4 + 6 |
| No Undo / no nav collapse | out of scope |

## Execution

Plan saved. **Executing inline** in this session (matches prior settings polish deliveries).
