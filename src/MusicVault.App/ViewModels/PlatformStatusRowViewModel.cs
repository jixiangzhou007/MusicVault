using CommunityToolkit.Mvvm.ComponentModel;
using MusicVault.App.Localization;
using MusicVault.Core.Enums;
using MusicVault.Services.Api;
using MusicVault.Services.Platform;

namespace MusicVault.App.ViewModels;

public partial class PlatformStatusRowViewModel : ObservableObject
{
    private readonly ILocalizationService _l10n;
    private readonly PlatformCapabilityRow _row;

    public PlatformStatusRowViewModel(
        PlatformCapabilityRow row,
        IReadOnlyCollection<string> loggedInSources,
        bool unifiedApiConfigured,
        ILocalizationService l10n)
    {
        _l10n = l10n;
        _row = row;
        Platform = row.Platform;
        Level = row.Level;
        IsEnabled = row.IsEnabled;
        IsLoggedIn = MusicApiLoggedInSources.IsPlatformLoggedIn(row.Platform, loggedInSources);
        IsAvailable = ComputeIsAvailable(row, loggedInSources, unifiedApiConfigured);
    }

    public MusicPlatform Platform { get; }
    public PlatformCapabilityLevel Level { get; }
    public bool IsEnabled { get; }

    public string DisplayName => ServiceMessageLocalizer.Resolve(_l10n, _row.DisplayName);
    public string Summary => ServiceMessageLocalizer.Resolve(_l10n, _row.Summary);
    public string LevelLabel => ServiceMessageLocalizer.Resolve(_l10n, PlatformCapabilityCatalog.LevelLabel(Level));

    /// <summary>go-music-api 上该平台 Cookie 有效（QQ 与 qq_wx 互通）。</summary>
    public bool IsLoggedIn { get; }

    /// <summary>发现页 chip 显示为绿色可用态。</summary>
    public bool IsAvailable { get; }

    public bool IsHealthy => Level is PlatformCapabilityLevel.Full or PlatformCapabilityLevel.Download
                             or PlatformCapabilityLevel.SearchOnly;

    public bool NeedsAttention => Level is PlatformCapabilityLevel.Unconfigured
                                  or PlatformCapabilityLevel.Disabled;

    public bool SupportsLogin => MusicApiLoggedInSources.TryGetSourceKey(Platform) is not null;

    public bool ShowLoginAction => SupportsLogin && !IsLoggedIn;

    public string LoginStatusText => !SupportsLogin
        ? _l10n.Get("Discover.Platform.NoLoginRequired")
        : IsLoggedIn
            ? _l10n.Get("Discover.Platform.LoggedIn")
            : _l10n.Get("Discover.Platform.NotLoggedIn");

    public bool LoginStatusIsSuccess => SupportsLogin && IsLoggedIn;
    public bool LoginStatusIsWarning => SupportsLogin && !IsLoggedIn;
    public bool LoginStatusIsInfo => !SupportsLogin;

    public string AvailabilityStatusText => IsAvailable
        ? _l10n.Get("Discover.Platform.Available")
        : _l10n.Get("Discover.Platform.Unavailable");
    public bool AvailabilityStatusIsSuccess => IsAvailable;
    public bool AvailabilityStatusIsError => !IsAvailable;

    public void RefreshLocalizedStrings()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(LevelLabel));
        OnPropertyChanged(nameof(LoginStatusText));
        OnPropertyChanged(nameof(AvailabilityStatusText));
    }

    private static bool ComputeIsAvailable(
        PlatformCapabilityRow row,
        IReadOnlyCollection<string> loggedIn,
        bool unifiedApiConfigured)
    {
        if (!row.IsEnabled)
            return false;

        return row.Level switch
        {
            PlatformCapabilityLevel.Disabled or PlatformCapabilityLevel.Unconfigured => false,
            PlatformCapabilityLevel.SearchOnly => false,
            PlatformCapabilityLevel.Full when row.Platform == MusicPlatform.All => unifiedApiConfigured,
            PlatformCapabilityLevel.Full => RequiresCookieLogin(row)
                ? MusicApiLoggedInSources.IsPlatformLoggedIn(row.Platform, loggedIn)
                : true,
            PlatformCapabilityLevel.Download => true,
            _ => false
        };
    }

    private static bool RequiresCookieLogin(PlatformCapabilityRow row) =>
        row.Level == PlatformCapabilityLevel.Full
        && MusicApiLoggedInSources.TryGetSourceKey(row.Platform) is not null
        && GoMusicApiClient.IsConfigured;
}
