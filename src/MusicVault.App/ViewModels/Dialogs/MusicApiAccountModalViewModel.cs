using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;
using MusicVault.Services;
using MusicVault.Services.Api;
using MusicVault.Services.Http;
using MusicVault.Services.Platform;

namespace MusicVault.App.ViewModels.Dialogs;

public sealed partial class MusicApiAccountModalViewModel : ViewModelBase
{
    private readonly IFormDrawerHost _host;
    private readonly string? _preferredSource;
    private CancellationTokenSource? _pollCts;
    private static readonly string[] PreferredSourceOrder =
    [
        "netease",
        "qq",
        "qq_wx",
        "kugou"
    ];

    public ObservableCollection<MusicApiLoginSourceOption> Sources { get; } = [];

    [ObservableProperty] private string _statusText;
    [ObservableProperty] private byte[]? _qrImageBytes;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasQrImage;
    [ObservableProperty] private string _cookieSummary;

    public string ModalTitle => L("Settings.Online.PlatformLogin");

    public string ModalSubtitle => L("Settings.QrLogin.ModalSubtitle");

    public string CloseToolTip => L("Sidebar.CloseToolTip");

    public string CloseButtonText => L("About.Close");

    public MusicApiAccountModalViewModel(IFormDrawerHost host, string? preferredSource = null)
    {
        _host = host;
        _preferredSource = preferredSource?.Trim();
        StatusText = L("Settings.QrLogin.InitialStatus");
        CookieSummary = L("Settings.QrLogin.CookieUnknown");
        App.Localization.CultureChanged += OnCultureChanged;
        _ = InitializeAsync();
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(ModalTitle));
        OnPropertyChanged(nameof(ModalSubtitle));
        OnPropertyChanged(nameof(CloseToolTip));
        OnPropertyChanged(nameof(CloseButtonText));
    }

    partial void OnIsBusyChanged(bool value)
    {
        foreach (var source in Sources)
            source.LoginCommand.NotifyCanExecuteChanged();
    }

    private async Task InitializeAsync()
    {
        await LoadSourcesAsync().ConfigureAwait(true);
        await RefreshCookieSummaryAsync().ConfigureAwait(true);
        await TryStartPreferredSourceAsync().ConfigureAwait(true);
    }

    private async Task LoadSourcesAsync()
    {
        var defaultItems = CreateDefaultSources().ToList();
        var itemMap = new Dictionary<string, MusicApiLoginSourceOption>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in defaultItems)
            itemMap[item.Source] = item;

        if (GoMusicApiAccountClient.IsConfigured)
        {
            try
            {
                var remote = await GoMusicApiAccountClient.GetQrLoginSourceDescriptorsAsync().ConfigureAwait(true);
                if (remote.Count > 0)
                {
                    foreach (var descriptor in remote)
                    {
                        if (string.IsNullOrWhiteSpace(descriptor.Source))
                            continue;

                        var source = descriptor.Source.Trim();
                        var label = ServiceMessageCodec.IsEncoded(descriptor.Label)
                            ? ServiceMessageLocalizer.Resolve(App.Localization, descriptor.Label.Trim())
                            : LabelForSource(source);
                        itemMap[source] = new MusicApiLoginSourceOption(source, label, this);
                    }
                }
            }
            catch
            {
                // 远端读取失败时保留默认全量平台列表
            }
        }

        var ordered = PreferredSourceOrder
            .Where(s => itemMap.ContainsKey(s))
            .Select(s => itemMap[s])
            .ToList();
        foreach (var kv in itemMap)
        {
            if (PreferredSourceOrder.Contains(kv.Key, StringComparer.OrdinalIgnoreCase))
                continue;
            ordered.Add(kv.Value);
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Sources.Clear();
            foreach (var item in ordered)
                Sources.Add(item);
        });
    }

    private async Task TryStartPreferredSourceAsync()
    {
        var source = ResolvePreferredSource(_preferredSource);
        if (string.IsNullOrWhiteSpace(source))
            return;

        await RunLoginFlowAsync(source).ConfigureAwait(true);
    }

    private string? ResolvePreferredSource(string? preferred)
    {
        if (string.IsNullOrWhiteSpace(preferred))
            return null;

        var normalized = preferred.Trim().ToLowerInvariant();
        if (Sources.Any(s => string.Equals(s.Source, normalized, StringComparison.OrdinalIgnoreCase)))
            return Sources.First(s => string.Equals(s.Source, normalized, StringComparison.OrdinalIgnoreCase)).Source;

        if (normalized is "qq" or "qqmusic")
        {
            var qq = Sources.FirstOrDefault(s => string.Equals(s.Source, "qq", StringComparison.OrdinalIgnoreCase))
                     ?? Sources.FirstOrDefault(s => string.Equals(s.Source, "qq_wx", StringComparison.OrdinalIgnoreCase));
            return qq?.Source;
        }

        return null;
    }

    [RelayCommand]
    private async Task StartLoginAsync(string? source)
    {
        if (string.IsNullOrWhiteSpace(source) || IsBusy)
            return;

        await RunLoginFlowAsync(source).ConfigureAwait(true);
    }

    internal async Task RunLoginFlowAsync(string source)
    {
        StopPolling();
        IsBusy = true;
        QrImageBytes = null;
        HasQrImage = false;
        StatusText = L("Settings.QrLogin.Preparing");

        try
        {
            var baseUrl = MusicApiConfig.Resolve();
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                StatusText = L("Settings.QrLogin.ApiNotConfigured");
                return;
            }

            var prepared = await PlatformConnectionTester.PrepareUnifiedApiAsync(baseUrl).ConfigureAwait(true);
            if (!prepared.Ok)
            {
                StatusText = ServiceMessageLocalizer.Resolve(App.Localization, prepared.Message);
                return;
            }

            var created = await GoMusicApiAccountClient.TryCreateQrLoginAsync(source).ConfigureAwait(true);
            if (created.Session == null)
            {
                var error = ServiceMessageLocalizer.Resolve(App.Localization, created.Error)
                    is { Length: > 0 } resolved
                        ? resolved
                        : L("Settings.QrLogin.UnknownError");
                StatusText = string.Format(L("Settings.QrLogin.CreateFailed"), LabelForSource(source), error);
                return;
            }

            var session = created.Session;
            var bytes = await LoadQrImageBytesAsync(session.LoginUrl).ConfigureAwait(true);
            if (bytes == null || bytes.Length == 0)
            {
                var link = session.LoginUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                    ? L("Settings.QrLogin.InvalidQrImage")
                    : session.LoginUrl;
                StatusText = string.Format(L("Settings.QrLogin.LoadQrFailed"), LabelForSource(source), link);
                return;
            }

            QrImageBytes = bytes;
            HasQrImage = true;
            StatusText = string.Format(L("Settings.QrLogin.ScanPrompt"), LabelForSource(source));

            _pollCts = new CancellationTokenSource();
            _ = PollLoginAsync(session, _pollCts.Token);
        }
        catch (Exception ex)
        {
            StatusText = ServiceMessageLocalizer.Format(App.Localization, "Settings.QrLogin.Failed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Close()
    {
        StopPolling();
        _host.Complete(null);
    }

    private async Task PollLoginAsync(QrLoginSession session, CancellationToken ct)
    {
        var pollInterval = GoMusicApiAccountClient.RequiresLongPollQr(session.Source)
            ? TimeSpan.FromMilliseconds(500)
            : TimeSpan.FromSeconds(2);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await GoMusicApiAccountClient
                    .PollQrLoginAsync(session.Source, session.Key, ct)
                    .ConfigureAwait(false);

                var terminal = false;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var label = LabelForSource(session.Source);
                    var message = ServiceMessageLocalizer.Resolve(App.Localization, result.Message);
                    switch (result.State)
                    {
                        case QrLoginPollState.Succeeded:
                            StatusText = message;
                            StopPolling();
                            HasQrImage = false;
                            QrImageBytes = null;
                            _ = OnLoginSucceededAsync();
                            terminal = true;
                            break;
                        case QrLoginPollState.Expired:
                            StatusText = string.Format(L("Settings.QrLogin.ExpiredRescan"), label, message);
                            StopPolling();
                            HasQrImage = false;
                            QrImageBytes = null;
                            terminal = true;
                            break;
                        case QrLoginPollState.Failed:
                            StatusText = string.Format(L("Settings.QrLogin.StatusLine"), label, message);
                            StopPolling();
                            HasQrImage = false;
                            QrImageBytes = null;
                            terminal = true;
                            break;
                        case QrLoginPollState.Waiting:
                            StatusText = string.Format(L("Settings.QrLogin.StatusLine"), label, message);
                            break;
                    }
                });

                if (terminal)
                    break;

                await Task.Delay(pollInterval, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                    StatusText = string.Format(
                        L("Settings.QrLogin.PollFailed"),
                        LabelForSource(session.Source),
                        ServiceMessageLocalizer.Resolve(App.Localization, ex.Message)));
                await Task.Delay(pollInterval, ct).ConfigureAwait(false);
            }
        }
    }

    private async Task OnLoginSucceededAsync()
    {
        try
        {
            var cookies = await GoMusicApiAccountClient.GetCookiesAsync().ConfigureAwait(true);
            if (cookies.Count > 0)
                await MusicApiSessionHelper.SaveCookiesBackupAsync(cookies).ConfigureAwait(true);
        }
        catch
        {
            // 备份失败不影响登录成功提示
        }

        await RefreshCookieSummaryAsync().ConfigureAwait(true);
        PlatformLoginStateNotifier.NotifyChanged();
    }

    private async Task RefreshCookieSummaryAsync()
    {
        if (!GoMusicApiAccountClient.IsConfigured)
        {
            await SetCookieSummaryAsync(L("Settings.QrLogin.CookieApiNotConfigured")).ConfigureAwait(true);
            return;
        }

        var cookies = await GoMusicApiAccountClient.GetCookiesAsync().ConfigureAwait(true);
        if (cookies.Count == 0)
        {
            await SetCookieSummaryAsync(L("Settings.QrLogin.CookieEmpty")).ConfigureAwait(true);
            return;
        }

        var labels = string.Join(
            App.Localization.Culture.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? ", " : "、",
            cookies.Keys.Select(LabelForSource));
        await SetCookieSummaryAsync(string.Format(L("Settings.QrLogin.CookieLoggedIn"), labels))
            .ConfigureAwait(true);
    }

    private async Task SetCookieSummaryAsync(string text)
    {
        await Dispatcher.UIThread.InvokeAsync(() => { CookieSummary = text; });
    }

    private IEnumerable<MusicApiLoginSourceOption> CreateDefaultSources() =>
    [
        new("netease", LabelForSource("netease"), this),
        new("qq", LabelForSource("qq"), this),
        new("qq_wx", LabelForSource("qq_wx"), this),
        new("kugou", LabelForSource("kugou"), this)
    ];

    private static async Task<byte[]?> LoadQrImageBytesAsync(string loginUrl)
    {
        var embedded = GoMusicApiAccountClient.TryDecodeDataUrlImage(loginUrl);
        if (embedded is { Length: > 0 })
            return embedded;

        if (GoMusicApiAccountClient.IsDirectQrImageUrl(loginUrl))
        {
            try
            {
                var direct = await MusicSearchHttp.Client.GetByteArrayAsync(loginUrl).ConfigureAwait(false);
                if (LooksLikeImageBytes(direct))
                    return direct;
            }
            catch
            {
                if (GoMusicApiAccountClient.IsWeChatOAuthQrImageUrl(loginUrl))
                    return null;
            }
        }

        if (!loginUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return null;

        if (GoMusicApiAccountClient.IsWeChatOAuthQrImageUrl(loginUrl))
            return null;

        var qrUrl =
            $"https://api.qrserver.com/v1/create-qr-code/?size=220x220&data={Uri.EscapeDataString(loginUrl)}";
        try
        {
            return await MusicSearchHttp.Client.GetByteArrayAsync(qrUrl).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private static bool LooksLikeImageBytes(byte[]? bytes)
    {
        if (bytes is not { Length: >= 4 })
            return false;

        if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            return true;
        if (bytes[0] == 0xFF && bytes[1] == 0xD8)
            return true;
        if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46)
            return true;
        return bytes.Length >= 12
               && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
               && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50;
    }

    private static string LabelForSource(string source)
    {
        var key = source.ToLowerInvariant() switch
        {
            "netease" => "Login.Source.NetEase",
            "qq" => "Login.Source.Qq",
            "qq_wx" => "Login.Source.QqWx",
            "kugou" => "Login.Source.Kugou",
            "kuwo" => "Login.Source.Kuwo",
            "migu" => "Login.Source.Migu",
            "soda" => "Login.Source.Soda",
            "bilibili" => "Login.Source.Bilibili",
            _ => null
        };
        return key != null ? L(key) : source;
    }

    private static string L(string key) => App.Localization.Get(key);

    private void StopPolling()
    {
        _pollCts?.Cancel();
        _pollCts?.Dispose();
        _pollCts = null;
    }
}

public sealed partial class MusicApiLoginSourceOption : ObservableObject
{
    private readonly MusicApiAccountModalViewModel _modal;

    public string Source { get; }
    public string Label { get; }

    public MusicApiLoginSourceOption(string source, string label, MusicApiAccountModalViewModel modal)
    {
        Source = source;
        Label = label;
        _modal = modal;
    }

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private Task LoginAsync() => _modal.RunLoginFlowAsync(Source);

    private bool CanLogin() => !_modal.IsBusy;
}
