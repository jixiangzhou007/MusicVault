using MusicVault.Services;
using MusicVault.Services.Api;
using MusicVault.Services.Platform;

namespace MusicVault.App.Services;

public enum OnlineApiHealthState
{
    Unknown,
    Checking,
    Healthy,
    Unhealthy,
    NotConfigured
}

/// <summary>统一 API + 扩展网易云 API 双通道连通性（启动与设置变更后刷新）。</summary>
public sealed class OnlineApiHealthService
{
    public static OnlineApiHealthService Instance { get; } = new();

    private const int FailureThreshold = 2;
    private static readonly TimeSpan MinRefreshInterval = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(25);

    private int _refreshGate;
    private int _unifiedConsecutiveFailures;
    private int _netEaseConsecutiveFailures;
    private DateTime _lastRefreshUtc = DateTime.MinValue;
    private int _pendingRefresh;

    public OnlineApiHealthState UnifiedState { get; private set; } = OnlineApiHealthState.Unknown;
    public OnlineApiHealthState NetEaseState { get; private set; } = OnlineApiHealthState.Unknown;
    public string UnifiedDetail { get; private set; } = string.Empty;
    public string NetEaseDetail { get; private set; } = string.Empty;
    public string? UnifiedUrl { get; private set; }
    public string? NetEaseUrl { get; private set; }

    /// <summary>聚合状态（取两通道中更严重者，供状态栏/发现页兼容）。</summary>
    public OnlineApiHealthState State => CombineState(UnifiedState, NetEaseState);

    public string DetailMessage { get; private set; } = string.Empty;

    public DateTime LastRefreshUtc => _lastRefreshUtc;

    public event EventHandler? Changed;

    public Task RefreshAsync(CancellationToken ct = default) =>
        RefreshAsync(force: false, ct);

    public async Task RefreshAsync(bool force, CancellationToken ct = default)
    {
        if (!force
            && State == OnlineApiHealthState.Healthy
            && DateTime.UtcNow - _lastRefreshUtc < MinRefreshInterval)
            return;

        if (Interlocked.CompareExchange(ref _refreshGate, 1, 0) != 0)
        {
            Interlocked.Exchange(ref _pendingRefresh, 1);
            return;
        }

        var silent = !force && State == OnlineApiHealthState.Healthy;
        var previousUnified = UnifiedState;
        var previousNetEase = NetEaseState;
        var previousUnifiedDetail = UnifiedDetail;
        var previousNetEaseDetail = NetEaseDetail;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(ProbeTimeout);

        try
        {
            if (!silent)
            {
                SetChannelStates(OnlineApiHealthState.Checking, OnlineApiHealthState.Checking, string.Empty, string.Empty);
            }

            await RefreshUnifiedChannelAsync(timeout.Token, previousUnified, previousUnifiedDetail, silent)
                .ConfigureAwait(false);
            await RefreshNetEaseChannelAsync(timeout.Token, previousNetEase, previousNetEaseDetail, silent)
                .ConfigureAwait(false);

            UpdateAggregateDetail();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            ApplyChannelFailure(
                UnifiedState,
                NetEaseState,
                ServiceMessageCodec.Key("Api.Probe.Timeout"),
                ServiceMessageCodec.Key("Api.Probe.Timeout"),
                previousUnified,
                previousNetEase,
                previousUnifiedDetail,
                previousNetEaseDetail,
                silent);
            UpdateAggregateDetail();
        }
        catch (Exception ex)
        {
            ApplyChannelFailure(
                UnifiedState,
                NetEaseState,
                ex.Message,
                ex.Message,
                previousUnified,
                previousNetEase,
                previousUnifiedDetail,
                previousNetEaseDetail,
                silent);
            UpdateAggregateDetail();
        }
        finally
        {
            _lastRefreshUtc = DateTime.UtcNow;
            Interlocked.Exchange(ref _refreshGate, 0);

            if (Interlocked.Exchange(ref _pendingRefresh, 0) == 1)
                _ = RefreshAsync(force, ct);
        }
    }

    public void MarkHealthyFromSuccessfulRequest(string? hint = null)
    {
        _unifiedConsecutiveFailures = 0;
        _lastRefreshUtc = DateTime.UtcNow;
        UnifiedState = OnlineApiHealthState.Healthy;
        UnifiedDetail = hint ?? ServiceMessageCodec.Key("Api.Connection.Ok");
        UnifiedUrl = MusicApiConfig.Resolve();
        UpdateAggregateDetail();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>搜索路径确认统一 API 失败时调用（避免「其它源有结果」误标健康）。</summary>
    public void NoteUnifiedSearchFailure()
    {
        _unifiedConsecutiveFailures++;
        _lastRefreshUtc = DateTime.UtcNow;
        if (ShouldMarkUnhealthy(UnifiedState, _unifiedConsecutiveFailures))
        {
            UnifiedState = OnlineApiHealthState.Unhealthy;
            UnifiedDetail = ServiceMessageCodec.Key("Api.HealthCheck.NoResponse");
        }

        UpdateAggregateDetail();
        Changed?.Invoke(this, EventArgs.Empty);
        _ = RefreshAsync(force: true);
    }

    public void MarkNetEaseHealthyFromSuccessfulRequest(string? hint = null)
    {
        _netEaseConsecutiveFailures = 0;
        _lastRefreshUtc = DateTime.UtcNow;
        NetEaseState = OnlineApiHealthState.Healthy;
        NetEaseDetail = hint
                        ?? MusicServiceEndpoints.ResolveNetEaseApi()
                        ?? ServiceMessageCodec.Key("Api.Connection.Ok");
        NetEaseUrl = MusicServiceEndpoints.ResolveNetEaseApi();
        UpdateAggregateDetail();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private async Task RefreshUnifiedChannelAsync(
        CancellationToken ct,
        OnlineApiHealthState previousState,
        string previousDetail,
        bool silent)
    {
        var configured = MusicApiConfig.Resolve();
        if (string.IsNullOrWhiteSpace(configured))
        {
            _unifiedConsecutiveFailures = 0;
            UnifiedState = OnlineApiHealthState.NotConfigured;
            UnifiedDetail = string.Empty;
            UnifiedUrl = null;
            return;
        }

        var resolved = await PlatformConnectionTester
            .ResolveHealthyUnifiedBaseAsync(configured, ct)
            .ConfigureAwait(false);

        UnifiedUrl = configured.Trim().TrimEnd('/');
        var configuredUrl = UnifiedUrl;
        if (resolved != null)
        {
            _unifiedConsecutiveFailures = 0;
            UnifiedState = OnlineApiHealthState.Healthy;
            UnifiedUrl = resolved;
            MusicApiRuntime.ApplyResolvedBaseUrl(resolved);
            UnifiedDetail = string.Equals(resolved, configuredUrl, StringComparison.OrdinalIgnoreCase)
                ? ServiceMessageCodec.Key("Api.Connection.Ok")
                : ServiceMessageCodec.Format("Api.Connection.OkFallbackPort", resolved);
            return;
        }

        _unifiedConsecutiveFailures++;
        if (ShouldMarkUnhealthy(previousState, _unifiedConsecutiveFailures))
        {
            UnifiedState = OnlineApiHealthState.Unhealthy;
            UnifiedDetail = ServiceMessageCodec.Key("Api.HealthCheck.NoResponse");
        }
        else if (silent && previousState == OnlineApiHealthState.Healthy)
        {
            UnifiedState = previousState;
            UnifiedDetail = previousDetail;
        }
        else
        {
            UnifiedState = OnlineApiHealthState.Unhealthy;
            UnifiedDetail = ServiceMessageCodec.Key("Api.HealthCheck.NoResponse");
        }
    }

    private async Task RefreshNetEaseChannelAsync(
        CancellationToken ct,
        OnlineApiHealthState previousState,
        string previousDetail,
        bool silent)
    {
        BundledNetEaseApiHost.Instance.Discover(AppContext.BaseDirectory);
        NetEaseUrl = MusicServiceEndpoints.ResolveNetEaseApi();
        if (string.IsNullOrWhiteSpace(NetEaseUrl) && BundledNetEaseApiHost.Instance.HasBundledApi)
            NetEaseUrl = BundledNetEaseApiHost.DefaultBaseUrl;

        if (string.IsNullOrWhiteSpace(NetEaseUrl))
        {
            _netEaseConsecutiveFailures = 0;
            NetEaseState = BundledNetEaseApiHost.Instance.HasBundledApi
                ? OnlineApiHealthState.NotConfigured
                : OnlineApiHealthState.NotConfigured;
            NetEaseDetail = string.Empty;
            return;
        }

        var result = await PlatformConnectionTester
            .ProbeNetEaseApiHealthAsync(NetEaseUrl, ct)
            .ConfigureAwait(false);

        if (result.Ok)
        {
            _netEaseConsecutiveFailures = 0;
            NetEaseState = OnlineApiHealthState.Healthy;
            NetEaseDetail = ServiceMessageCodec.Key("Api.Connection.Ok");
            NetEaseUrl = result.Message.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? result.Message
                : NetEaseUrl;
            return;
        }

        _netEaseConsecutiveFailures++;
        if (ShouldMarkUnhealthy(previousState, _netEaseConsecutiveFailures))
        {
            NetEaseState = OnlineApiHealthState.Unhealthy;
            NetEaseDetail = result.Message;
        }
        else if (silent && previousState == OnlineApiHealthState.Healthy)
        {
            NetEaseState = previousState;
            NetEaseDetail = previousDetail;
        }
        else
        {
            NetEaseState = OnlineApiHealthState.Unhealthy;
            NetEaseDetail = result.Message;
        }
    }

    private void ApplyChannelFailure(
        OnlineApiHealthState unifiedState,
        OnlineApiHealthState netEaseState,
        string unifiedDetail,
        string netEaseDetail,
        OnlineApiHealthState previousUnified,
        OnlineApiHealthState previousNetEase,
        string previousUnifiedDetail,
        string previousNetEaseDetail,
        bool silent)
    {
        _unifiedConsecutiveFailures++;
        _netEaseConsecutiveFailures++;

        if (ShouldMarkUnhealthy(previousUnified, _unifiedConsecutiveFailures) || !silent)
        {
            UnifiedState = OnlineApiHealthState.Unhealthy;
            UnifiedDetail = unifiedDetail;
        }
        else if (previousUnified == OnlineApiHealthState.Healthy)
        {
            UnifiedState = previousUnified;
            UnifiedDetail = previousUnifiedDetail;
        }

        if (ShouldMarkUnhealthy(previousNetEase, _netEaseConsecutiveFailures) || !silent)
        {
            NetEaseState = OnlineApiHealthState.Unhealthy;
            NetEaseDetail = netEaseDetail;
        }
        else if (previousNetEase == OnlineApiHealthState.Healthy)
        {
            NetEaseState = previousNetEase;
            NetEaseDetail = previousNetEaseDetail;
        }
    }

    private void SetChannelStates(
        OnlineApiHealthState unified,
        OnlineApiHealthState netEase,
        string unifiedDetail,
        string netEaseDetail)
    {
        UnifiedState = unified;
        NetEaseState = netEase;
        UnifiedDetail = unifiedDetail;
        NetEaseDetail = netEaseDetail;
        UpdateAggregateDetail();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateAggregateDetail()
    {
        DetailMessage = State switch
        {
            OnlineApiHealthState.Healthy => UnifiedDetail,
            OnlineApiHealthState.NotConfigured when UnifiedState == OnlineApiHealthState.NotConfigured =>
                NetEaseDetail,
            _ => string.Join(" · ", new[] { UnifiedDetail, NetEaseDetail }.Where(s => !string.IsNullOrWhiteSpace(s)))
        };
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static bool ShouldMarkUnhealthy(OnlineApiHealthState previous, int consecutiveFailures) =>
        previous == OnlineApiHealthState.Healthy
            ? consecutiveFailures >= FailureThreshold
            : consecutiveFailures >= 1;

    private static OnlineApiHealthState CombineState(OnlineApiHealthState a, OnlineApiHealthState b)
    {
        int Rank(OnlineApiHealthState s) => s switch
        {
            OnlineApiHealthState.Unhealthy => 5,
            OnlineApiHealthState.NotConfigured => 4,
            OnlineApiHealthState.Checking => 3,
            OnlineApiHealthState.Unknown => 2,
            OnlineApiHealthState.Healthy => 1,
            _ => 0
        };

        return Rank(a) >= Rank(b) ? a : b;
    }
}
