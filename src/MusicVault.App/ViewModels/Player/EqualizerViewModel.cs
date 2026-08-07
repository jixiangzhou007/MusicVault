using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;
using MusicVault.App.Services;
using MusicVault.Core.Interfaces;
using MusicVault.Services;

namespace MusicVault.App.ViewModels.Player;

/// <summary>均衡器模态面板 ViewModel</summary>
public partial class EqualizerViewModel : ViewModelBase
{
    private readonly AppSettingsService _settings;
    private readonly IAudioPlayerService _player;
    private readonly ILocalizationService _l10n;
    private bool _suppressPresetApply;
    private bool _suppressPresetOptionSync;

    public ObservableCollection<EqualizerBandViewModel> Bands { get; } = [];
    public ObservableCollection<EqualizerPresetOption> PresetOptions { get; } = [];

    [ObservableProperty] private bool _isEnabled = true;
    [ObservableProperty] private string _selectedPreset = EqualizerPresets.Custom;
    [ObservableProperty] private EqualizerPresetOption? _selectedPresetOption;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public string Title => _l10n.Get("Player.Equalizer.Title");
    public string EnableLabel => _l10n.Get("Player.Equalizer.Enable");
    public string DeleteLabel => _l10n.Get("Player.Equalizer.Delete");
    public string SaveLabel => _l10n.Get("Player.Equalizer.Save");
    public string ResetLabel => _l10n.Get("Player.Equalizer.Reset");
    public string CloseLabel => _l10n.Get("Player.Equalizer.Close");

    public event EventHandler? CloseRequested;

    public EqualizerViewModel(IAudioPlayerService player, AppSettingsService settings, ILocalizationService l10n)
    {
        _settings = settings;
        _player = player;
        _l10n = l10n;
        _l10n.CultureChanged += (_, _) => RefreshLocalizedStrings();
        RebuildPresetOptions();
        LoadFromSettings();
    }

    public void RefreshLocalizedStrings()
    {
        foreach (var option in PresetOptions)
            option.DisplayName = FormatPresetDisplayName(option.Id);
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(EnableLabel));
        OnPropertyChanged(nameof(DeleteLabel));
        OnPropertyChanged(nameof(SaveLabel));
        OnPropertyChanged(nameof(ResetLabel));
        OnPropertyChanged(nameof(CloseLabel));
        SyncSelectedPresetOption();
    }

    public void SetEqualizerBandGain(int bandIndex, double gainDb, bool fromUi = true)
    {
        if (bandIndex < 0 || bandIndex >= Bands.Count) return;
        gainDb = Math.Clamp(gainDb, -20, 20);
        Bands[bandIndex].Gain = gainDb;
        if (IsEnabled)
            _player.SetEqualizerGain(bandIndex, (float)gainDb);
        if (fromUi)
        {
            _suppressPresetApply = true;
            SelectedPreset = EqualizerPresets.Custom;
            _suppressPresetApply = false;
        }
        SaveSettings();
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Reset()
    {
        _suppressPresetApply = true;
        SelectedPreset = EqualizerPresets.Custom;
        _suppressPresetApply = false;
        ApplyGains(Enumerable.Repeat(0f, Bands.Count).ToArray());
        StatusMessage = _l10n.Get("Player.Equalizer.ResetFlat");
    }

    [RelayCommand]
    private void SavePreset()
    {
        _settings.Current.EqualizerCustomPresetName = EqualizerPresets.IsCustomPreset(SelectedPreset)
            ? EqualizerPresets.DefaultCustomPresetStorageKey
            : SelectedPreset;
        _settings.Current.EqualizerCustomGains = Bands.Select(b => (float)b.Gain).ToArray();
        _ = _settings.SaveAsync();
        StatusMessage = _l10n.Get("Player.Equalizer.PresetSaved");
    }

    [RelayCommand]
    private void DeletePreset()
    {
        if (SelectedPreset == EqualizerPresets.Custom)
        {
            StatusMessage = _l10n.Get("Player.Equalizer.CannotDeleteCustom");
            return;
        }
        _suppressPresetApply = true;
        SelectedPreset = EqualizerPresets.Custom;
        _suppressPresetApply = false;
        StatusMessage = _l10n.Get("Player.Equalizer.SwitchedToCustom");
    }

    partial void OnIsEnabledChanged(bool value)
    {
        _settings.Current.EqualizerEnabled = value;
        if (value)
        {
            for (var i = 0; i < Bands.Count; i++)
                _player.SetEqualizerGain(i, (float)Bands[i].Gain);
        }
        else
        {
            _player.ResetEqualizer();
        }
        _ = _settings.SaveAsync();
    }

    partial void OnSelectedPresetOptionChanged(EqualizerPresetOption? value)
    {
        if (_suppressPresetOptionSync || value == null) return;
        if (SelectedPreset == value.Id) return;
        SelectedPreset = value.Id;
    }

    public bool IsCustomPresetSelected => SelectedPreset == EqualizerPresets.Custom;

    partial void OnSelectedPresetChanged(string value)
    {
        SyncSelectedPresetOption();
        OnPropertyChanged(nameof(IsCustomPresetSelected));
        if (_suppressPresetApply || value == EqualizerPresets.Custom) return;
        var gains = EqualizerPresets.TryGetGains(value);
        if (gains == null) return;
        ApplyGains(gains);
        StatusMessage = FormatPresetAppliedMessage(value);
    }

    private void ApplyGains(IReadOnlyList<float> gains)
    {
        for (var i = 0; i < Bands.Count && i < gains.Count; i++)
        {
            Bands[i].Gain = gains[i];
            if (IsEnabled)
                _player.SetEqualizerGain(i, gains[i]);
        }
        SaveSettings();
    }

    private void LoadFromSettings()
    {
        var s = _settings.Current;
        IsEnabled = s.EqualizerEnabled;
        SelectedPreset = EqualizerPresets.NormalizePresetId(
            string.IsNullOrWhiteSpace(s.EqualizerPreset) ? EqualizerPresets.Custom : s.EqualizerPreset);

        var saved = s.EqualizerGains;
        if (saved.Length == 0)
            saved = new float[_player.EqualizerBandCount];

        Bands.Clear();
        for (var i = 0; i < _player.EqualizerBandCount; i++)
        {
            var gain = i < saved.Length ? saved[i] : 0f;
            if (!float.IsFinite(gain))
                gain = 0f;
            gain = Math.Clamp(gain, -20f, 20f);
            if (IsEnabled)
                _player.SetEqualizerGain(i, gain);
            Bands.Add(new EqualizerBandViewModel
            {
                BandIndex = i,
                Label = i < EqualizerPresets.BandLabels.Length ? EqualizerPresets.BandLabels[i] : $"{i}",
                Gain = gain
            });
        }

        if (SelectedPreset != EqualizerPresets.Custom)
        {
            var preset = EqualizerPresets.TryGetGains(SelectedPreset);
            if (preset != null)
                ApplyGains(preset);
        }

        SyncSelectedPresetOption();
    }

    private void SaveSettings()
    {
        _settings.Current.EqualizerGains = Bands.Select(b => (float)b.Gain).ToArray();
        _settings.Current.EqualizerPreset = SelectedPreset;
        _settings.Current.EqualizerEnabled = IsEnabled;
        _ = _settings.SaveAsync();
    }

    private void RebuildPresetOptions()
    {
        PresetOptions.Clear();
        foreach (var name in EqualizerPresets.BuiltInNames)
            PresetOptions.Add(new EqualizerPresetOption(name, FormatPresetDisplayName(name)));
    }

    private void SyncSelectedPresetOption()
    {
        var match = PresetOptions.FirstOrDefault(p => p.Id == SelectedPreset);
        if (ReferenceEquals(SelectedPresetOption, match)) return;
        _suppressPresetOptionSync = true;
        SelectedPresetOption = match;
        _suppressPresetOptionSync = false;
    }

    private string FormatPresetDisplayName(string id) => EqualizerPresetLocalizer.Format(_l10n, id);

    private string FormatPresetAppliedMessage(string id) =>
        string.Format(_l10n.Get("Player.Equalizer.PresetApplied"), EqualizerPresetLocalizer.Format(_l10n, id));
}

public sealed class EqualizerPresetOption(string id, string displayName)
{
    public string Id { get; } = id;
    public string DisplayName { get; set; } = displayName;
}
