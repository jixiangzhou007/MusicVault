using MusicVault.App.Services;
using MusicVault.Services;

namespace MusicVault.App.Localization;

/// <summary>基于字典的 zh-CN / en-US 本地化实现。</summary>
public sealed class LocalizationService : ILocalizationService
{
    public const string ZhCn = "zh-CN";
    public const string EnUs = "en-US";

    private readonly AppSettingsService _settings;
    private IReadOnlyDictionary<string, string> _strings = LocalizationResources.ZhCn;

    public LocalizationService(AppSettingsService settings)
    {
        _settings = settings;
        ApplyCulture(NormalizeCulture(settings.Current.Culture), persist: false);
    }

    public string Culture { get; private set; } = ZhCn;

    public event EventHandler? CultureChanged;

    public string Get(string key)
    {
        if (_strings.TryGetValue(key, out var v))
            return v;

        if (GuideLocalizationResources.TryGet(key, Culture, out v))
            return v;

        if (SettingsLocalizationResources.TryGet(key, Culture, out v))
            return v;

        if (DiscoverLocalizationResources.TryGet(key, Culture, out v))
            return v;

        if (LibraryLocalizationResources.TryGet(key, Culture, out v))
            return v;

        if (TransfersLocalizationResources.TryGet(key, Culture, out v))
            return v;

        if (MetadataLocalizationResources.TryGet(key, Culture, out v))
            return v;

        if (DesignSystemLocalizationResources.TryGet(key, Culture, out v))
            return v;

        if (ServiceMessages.All.TryGetValue(key, out var pair))
            return Culture == EnUs ? pair.EnUs : pair.ZhCn;

        return key;
    }

    public string GetPlatformLabel(MusicVault.Core.Enums.MusicPlatform platform) =>
        platform switch
        {
            MusicVault.Core.Enums.MusicPlatform.All => Get("Transfers.Platform.All"),
            MusicVault.Core.Enums.MusicPlatform.NetEase => Get("Platform.NetEase"),
            MusicVault.Core.Enums.MusicPlatform.QQMusic => Get("Platform.QQMusic"),
            MusicVault.Core.Enums.MusicPlatform.Kugou => Get("Platform.Kugou"),
            MusicVault.Core.Enums.MusicPlatform.Kuwo => Get("Platform.Kuwo"),
            MusicVault.Core.Enums.MusicPlatform.Migu => Get("Platform.Migu"),
            MusicVault.Core.Enums.MusicPlatform.MyFreeMp3 => Get("Platform.MyFreeMp3"),
            MusicVault.Core.Enums.MusicPlatform.Jbsou => Get("Platform.Jbsou"),
            MusicVault.Core.Enums.MusicPlatform.Soda => Get("Platform.Soda"),
            MusicVault.Core.Enums.MusicPlatform.NasLibrary => Get("Platform.NasLibrary"),
            _ => platform.ToString()
        };

    public async Task SetCultureAsync(string culture, CancellationToken ct = default)
    {
        var normalized = NormalizeCulture(culture);
        if (string.Equals(Culture, normalized, StringComparison.OrdinalIgnoreCase)
            && string.Equals(_settings.Current.Culture, normalized, StringComparison.OrdinalIgnoreCase))
            return;

        ApplyCulture(normalized, persist: true);
        await _settings.SaveAsync(ct).ConfigureAwait(false);
        CultureChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyCulture(string culture, bool persist)
    {
        Culture = culture;
        _strings = culture == EnUs ? LocalizationResources.EnUs : LocalizationResources.ZhCn;
        ServiceCulture.SetCurrent(culture);
        if (persist)
            _settings.Current.Culture = culture;
    }

    public static string NormalizeCulture(string? culture) =>
        string.Equals(culture, EnUs, StringComparison.OrdinalIgnoreCase) ? EnUs : ZhCn;

    public static int CultureToIndex(string culture) =>
        NormalizeCulture(culture) == EnUs ? 1 : 0;

    public static string IndexToCulture(int index) => index == 1 ? EnUs : ZhCn;
}
