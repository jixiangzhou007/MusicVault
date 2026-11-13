using CommunityToolkit.Mvvm.ComponentModel;
using MusicVault.App.Localization;
using MusicVault.App.Services;
using MusicVault.Core.Gatherers;

namespace MusicVault.App.ViewModels.Pages;

/// <summary>设置页采集源行</summary>
public partial class GathererItemViewModel : ObservableObject
{
    public string Id { get; }
    public bool IsConfigurableUrl { get; }

    public bool CanOpenUrlDialog =>
        Id is MusicGathererIds.UnifiedGoMusicApi
            or MusicGathererIds.NetEaseApi
            or MusicGathererIds.QqMusicApi;

    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private string _baseUrl = string.Empty;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private string? _probeStatusRaw;

    private readonly ILocalizationService _l10n;
    private readonly string _displayNameRaw;
    private readonly string _descriptionRaw;
    private readonly Action? _onChanged;

    public string DisplayName => ServiceMessageLocalizer.Resolve(_l10n, _displayNameRaw);
    public string Description => ServiceMessageLocalizer.Resolve(_l10n, _descriptionRaw);
    public bool IsProbeFailed => OnlineAdvancedExpandPolicy.IsGathererProbeFailedFromRaw(ProbeStatusRaw);

    public GathererItemViewModel(
        MusicGathererDefinition definition,
        ILocalizationService l10n,
        Action? onChanged = null)
    {
        _l10n = l10n;
        _onChanged = onChanged;
        Id = definition.Id;
        _displayNameRaw = definition.DisplayName;
        _descriptionRaw = definition.Description;
        IsConfigurableUrl = definition.IsConfigurableUrl;
        _isEnabled = definition.Enabled;
        _baseUrl = definition.ConfiguredUrl ?? definition.DefaultBaseUrl ?? string.Empty;
        _statusText = string.Empty;
        ApplyProbeResult(definition.LastProbeStatus);
    }

    public void ApplyStatus(string status) => StatusText = status;

    /// <summary>写入探测原始编码（svc:…），供展开策略判断失败；UI 显示本地化文案。</summary>
    public void ApplyProbeResult(string? rawStatus)
    {
        if (string.IsNullOrWhiteSpace(rawStatus) || rawStatus == "—")
        {
            ProbeStatusRaw = null;
            StatusText = "—";
            OnPropertyChanged(nameof(IsProbeFailed));
            return;
        }

        ProbeStatusRaw = rawStatus;
        StatusText = ServiceMessageLocalizer.Resolve(_l10n, rawStatus);
        OnPropertyChanged(nameof(IsProbeFailed));
    }

    public void RefreshLocalizedStrings()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(Description));
        if (!string.IsNullOrWhiteSpace(ProbeStatusRaw))
            StatusText = ServiceMessageLocalizer.Resolve(_l10n, ProbeStatusRaw);
    }

    partial void OnIsEnabledChanged(bool value) => _onChanged?.Invoke();
}
