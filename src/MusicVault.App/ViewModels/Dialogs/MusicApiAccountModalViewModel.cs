using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Constants;
using MusicVault.App.Localization;
using MusicVault.Core.Enums;
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
    private readonly HashSet<string> _remoteQrSources = new(StringComparer.OrdinalIgnoreCase);
    private string? _selectedSource;
    private bool _canRescan;

    public ObservableCollection<MusicApiLoginSourceOption> Sources { get; } = [];

    [ObservableProperty] private string _statusText;
    [ObservableProperty] private byte[]? _qrImageBytes;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasQrImage;
    [ObservableProperty] private string _cookieSummary;
    [ObservableProperty] private string _qrPlaceholderText;

    public string ModalTitle => L("Settings.Online.PlatformLogin");

    public string ModalSubtitle => L("Settings.QrLogin.ModalSubtitle");

    public string CloseToolTip => L("Sidebar.CloseToolTip");

    public string CloseButtonText => L("About.Close");

    public string RescanButtonText => L("Settings.QrLogin.Rescan");

    public bool CanRescan
    {
        get => _canRescan;
        private set
        {
            if (SetProperty(ref _canRescan, value))
                RescanCommand.NotifyCanExecuteChanged();
        }
    }

    public MusicApiAccountModalViewModel(IFormDrawerHost host, string? preferredSource = null)
    {
        _host = host;
        _preferredSource = preferredSource?.Trim();
        StatusText = L("Settings.QrLogin.InitialStatus");
        CookieSummary = L("Settings.QrLogin.CookieUnknown");
        QrPlaceholderText = L("Settings.QrLogin.QrPlaceholder");
        App.Localization.CultureChanged += OnCultureChanged;
        _ = InitializeAsync();
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(ModalTitle));
        OnPropertyChanged(nameof(ModalSubtitle));
        OnPropertyChanged(nameof(CloseToolTip));
        OnPropertyChanged(nameof(CloseButtonText));
        OnPropertyChanged(nameof(RescanButtonText));
        QrPlaceholderText = L("Settings.QrLogin.QrPlaceholder");
    }

    partial void OnIsBusyChanged(bool value)
    {
        foreach (var source in Sources)
            source.NotifyCanExecuteChanged();
        RescanCommand.NotifyCanExecuteChanged();
    }

    private async Task InitializeAsync()
    {
        await LoadSourcesAsync().ConfigureAwait(true);
        await RefreshCookieSummaryAsync().ConfigureAwait(true);
        await TryStartPreferredSourceAsync().ConfigureAwait(true);
    }

    private async Task LoadSourcesAsync()
    {
        _remoteQrSources.Clear();
        var itemMap = new Dictionary<string, MusicApiLoginSourceOption>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in GoMusicApiAccountClient.DisplaySourceOrder)
            itemMap[source] = CreateOption(source, supportsQr: false);

        if (GoMusicApiAccountClient.IsConfigured)
        {
            try
            {
                var remote = await GoMusicApiAccountClient.GetQrLoginSourceDescriptorsAsync().ConfigureAwait(true);
                foreach (var descriptor in remote)
                {
                    if (string.IsNullOrWhiteSpace(descriptor.Source))
                        continue;

                    var source = descriptor.Source.Trim();
                    _remoteQrSources.Add(source);
                    var label = ServiceMessageCodec.IsEncoded(descriptor.Label)
                        ? ServiceMessageLocalizer.Resolve(App.Localization, descriptor.Label.Trim())
                        : LabelForSource(source);

                    if (!itemMap.ContainsKey(source)
                        && GoMusicApiAccountClient.DisplaySourceOrder.Contains(
                            source, StringComparer.OrdinalIgnoreCase))
                        itemMap[source] = CreateOption(source, supportsQr: false, labelOverride: label);
                }
            }
            catch
            {
                // 远端失败时保留本地默认清单；SupportsQr 走 Known 回退
            }
        }

        foreach (var option in itemMap.Values)
        {
            var supports = SupportsQrFor(option.Source);
            option.SupportsQr = supports;
            option.Icon = IconForSource(
                option.Source,
                color: PlatformQrLoginSupport.ShowColorIcon(option.IsLoggedIn, supports));
            option.ToolTip = BuildToolTip(option.Source, supports, option.IsLoggedIn);
        }

        // 只展示与发现页对齐的清单；远端多出的 bilibili 等不入库（无正确图标且非扫码主路径）
        var ordered = GoMusicApiAccountClient.DisplaySourceOrder
            .Where(s => itemMap.ContainsKey(s))
            .Select(s => itemMap[s])
            .ToList();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Sources.Clear();
            foreach (var item in ordered)
                Sources.Add(item);
        });
    }

    private bool SupportsQrFor(string source) =>
        PlatformQrLoginSupport.IsSupported(
            source,
            _remoteQrSources,
            GoMusicApiAccountClient.IsConfigured);

    private MusicApiLoginSourceOption CreateOption(
        string source,
        bool supportsQr,
        string? labelOverride = null)
    {
        var label = labelOverride ?? LabelForSource(source);
        return new MusicApiLoginSourceOption(
            source,
            label,
            ShortLabelForSource(source),
            IconForSource(source, color: PlatformQrLoginSupport.ShowColorIcon(isLoggedIn: false, supportsQr)),
            supportsQr,
            BuildToolTip(source, supportsQr, isLoggedIn: false),
            this);
    }

    private static string ShortLabelForSource(string source) =>
        source.ToLowerInvariant() switch
        {
            "qq" => L("Login.Source.QqShort"),
            "qq_wx" => L("Login.Source.QqWxShort"),
            "netease" => L("Login.Source.NetEase"),
            "kugou" => L("Login.Source.Kugou"),
            "kuwo" => L("Login.Source.Kuwo"),
            "migu" => L("Login.Source.Migu"),
            "soda" => L("Login.Source.SodaShort"),
            "bilibili" => L("Login.Source.Bilibili"),
            _ => LabelForSource(source)
        };

    private static Bitmap IconForSource(string source, bool color)
    {
        var platform = PlatformForSource(source);
        return MusicPlatformIcons.ForPlatform(platform, color);
    }

    private static MusicPlatform PlatformForSource(string source) =>
        source.ToLowerInvariant() switch
        {
            "netease" => MusicPlatform.NetEase,
            "qq" or "qq_wx" or "qqmusic" => MusicPlatform.QQMusic,
            "kugou" => MusicPlatform.Kugou,
            "kuwo" => MusicPlatform.Kuwo,
            "migu" => MusicPlatform.Migu,
            "soda" => MusicPlatform.Soda,
            _ => MusicPlatform.All
        };

    private string BuildToolTip(string source, bool supportsQr, bool isLoggedIn)
    {
        var label = LabelForSource(source);
        if (isLoggedIn)
            return string.Format(L("Settings.QrLogin.LoggedInTip"), label);
        if (supportsQr)
            return string.Format(L("Settings.QrLogin.ScanTip"), label);
        return string.Format(L("Settings.QrLogin.UnsupportedTip"), label);
    }

    private async Task TryStartPreferredSourceAsync()
    {
        var source = ResolvePreferredSource(_preferredSource)
                     ?? Sources.FirstOrDefault(s => s.SupportsQr)?.Source;
        if (string.IsNullOrWhiteSpace(source))
            return;

        await SelectSourceAsync(source).ConfigureAwait(true);
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

    internal async Task SelectSourceAsync(string source)
    {
        if (string.IsNullOrWhiteSpace(source) || IsBusy)
            return;

        _selectedSource = source;
        foreach (var option in Sources)
            option.IsSelected = string.Equals(option.Source, source, StringComparison.OrdinalIgnoreCase);

        var selected = Sources.FirstOrDefault(s =>
            string.Equals(s.Source, source, StringComparison.OrdinalIgnoreCase));
        if (selected is not { SupportsQr: true })
        {
            StopPolling();
            QrImageBytes = null;
            HasQrImage = false;
            CanRescan = false;
            StatusText = selected == null
                ? L("Settings.QrLogin.InitialStatus")
                : string.Format(L("Settings.QrLogin.UnsupportedStatus"), selected.Label);
            return;
        }

        await RunLoginFlowAsync(source).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task StartLoginAsync(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return;
        await SelectSourceAsync(source).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanExecuteRescan))]
    private async Task RescanAsync()
    {
        if (string.IsNullOrWhiteSpace(_selectedSource))
            return;
        await RunLoginFlowAsync(_selectedSource).ConfigureAwait(true);
    }

    private bool CanExecuteRescan() => CanRescan && !IsBusy && !string.IsNullOrWhiteSpace(_selectedSource);

    internal async Task RunLoginFlowAsync(string source)
    {
        StopPolling();
        IsBusy = true;
        CanRescan = false;
        QrImageBytes = null;
        HasQrImage = false;
        StatusText = L("Settings.QrLogin.Preparing");

        try
        {
            var baseUrl = MusicApiConfig.Resolve();
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                StatusText = L("Settings.QrLogin.ApiNotConfigured");
                CanRescan = true;
                return;
            }

            var prepared = await PlatformConnectionTester.PrepareUnifiedApiAsync(baseUrl).ConfigureAwait(true);
            if (!prepared.Ok)
            {
                StatusText = ServiceMessageLocalizer.Resolve(App.Localization, prepared.Message);
                CanRescan = true;
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
                CanRescan = true;
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
                CanRescan = true;
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
            CanRescan = true;
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
                            CanRescan = false;
                            _ = OnLoginSucceededAsync();
                            terminal = true;
                            break;
                        case QrLoginPollState.Expired:
                            StatusText = string.Format(L("Settings.QrLogin.ExpiredRescan"), label, message);
                            StopPolling();
                            HasQrImage = false;
                            QrImageBytes = null;
                            CanRescan = true;
                            terminal = true;
                            break;
                        case QrLoginPollState.Failed:
                            StatusText = string.Format(L("Settings.QrLogin.StatusLine"), label, message);
                            StopPolling();
                            HasQrImage = false;
                            QrImageBytes = null;
                            CanRescan = true;
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
            await ApplyLoggedInStateAsync([], L("Settings.QrLogin.CookieApiNotConfigured")).ConfigureAwait(true);
            return;
        }

        var cookies = await GoMusicApiAccountClient.GetCookiesAsync().ConfigureAwait(true);
        if (cookies.Count == 0)
        {
            await ApplyLoggedInStateAsync([], L("Settings.QrLogin.CookieEmpty")).ConfigureAwait(true);
            return;
        }

        var labels = string.Join(
            App.Localization.Culture.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? ", " : "、",
            cookies.Keys.Select(LabelForSource));
        await ApplyLoggedInStateAsync(
                cookies.Keys,
                string.Format(L("Settings.QrLogin.CookieLoggedIn"), labels))
            .ConfigureAwait(true);
    }

    private async Task ApplyLoggedInStateAsync(IEnumerable<string> loggedInSources, string summary)
    {
        var set = new HashSet<string>(loggedInSources, StringComparer.OrdinalIgnoreCase);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            CookieSummary = summary;
            foreach (var option in Sources)
            {
                var loggedIn = set.Contains(option.Source)
                               || (string.Equals(option.Source, "qq", StringComparison.OrdinalIgnoreCase)
                                   && set.Contains("qq_wx"))
                               || (string.Equals(option.Source, "qq_wx", StringComparison.OrdinalIgnoreCase)
                                   && set.Contains("qq"));
                option.IsLoggedIn = loggedIn;
                option.Icon = IconForSource(
                    option.Source,
                    color: PlatformQrLoginSupport.ShowColorIcon(loggedIn, option.SupportsQr));
                option.ToolTip = BuildToolTip(option.Source, option.SupportsQr, loggedIn);
            }
        });
    }

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
    /// <summary>芯片下方短标签（如「微信」），完整名放 ToolTip。</summary>
    public string ShortLabel { get; }

    [ObservableProperty] private Bitmap _icon;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isLoggedIn;
    [ObservableProperty] private bool _supportsQr;
    [ObservableProperty] private string _toolTip;

    public MusicApiLoginSourceOption(
        string source,
        string label,
        string shortLabel,
        Bitmap icon,
        bool supportsQr,
        string toolTip,
        MusicApiAccountModalViewModel modal)
    {
        Source = source;
        Label = label;
        ShortLabel = shortLabel;
        _icon = icon;
        _supportsQr = supportsQr;
        _toolTip = toolTip;
        _modal = modal;
    }

    public bool CanSelect => !_modal.IsBusy;

    [RelayCommand(CanExecute = nameof(CanSelect))]
    private Task SelectAsync() => _modal.SelectSourceAsync(Source);

    public void NotifyCanExecuteChanged() => SelectCommand.NotifyCanExecuteChanged();
}
