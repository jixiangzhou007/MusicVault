using CommunityToolkit.Mvvm.ComponentModel;
using MusicVault.App.Localization;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Core.Nas;

namespace MusicVault.App.ViewModels;

/// <summary>首页左侧音乐库列表项</summary>
public partial class LibraryListItemViewModel : ObservableObject
{
    private string _trackCountSegmentFormat = "({0} 首)";
    private string _webDavSuffixFormat = "{0} · WebDAV";
    private string _nasSuffixFormat = "{0} · NAS";

    public MusicLibrary? Library { get; }
    public string Name { get; private set; }
    public string RootPath { get; private set; }

    [ObservableProperty] private int _trackCount;
    [ObservableProperty] private long _totalSizeBytes;

    public long? LibraryId => Library?.Id;
    public bool IsAllLibraries => Library is null;
    public bool IsNetworkSource =>
        Library?.SourceType is LibrarySourceType.NetworkNas or LibrarySourceType.NetworkWebDav;

    public bool IsLibraryDisabled => Library is { IsEnabled: false };

    /// <summary>列表主标题：全部音乐 / 路径最后一层文件夹名</summary>
    public string DisplayName
    {
        get
        {
            if (IsAllLibraries) return Name;
            var leaf = GetLeafFolderName(RootPath, Name);
            if (!IsNetworkSource)
                return leaf;

            return Library?.SourceType switch
            {
                LibrarySourceType.NetworkWebDav => string.Format(_webDavSuffixFormat, leaf),
                _ => string.Format(_nasSuffixFormat, leaf)
            };
        }
    }

    /// <summary>曲目数，如 (4首)</summary>
    public string TrackCountSegment => string.Format(_trackCountSegmentFormat, TrackCount);

    /// <summary>容量，统一换算为 GB 显示</summary>
    public string SizeGbText => FormatSizeGb(TotalSizeBytes);

    /// <summary>悬停时显示的完整路径（仅子库）</summary>
    public string? HoverPath => IsAllLibraries ? null : RootPath;

    public void RefreshLocalizedStrings(ILocalizationService l10n)
    {
        if (IsAllLibraries)
        {
            Name = l10n.Get("Library.AllTracks");
            RootPath = l10n.Get("Library.AllLibrariesSummary");
        }

        _trackCountSegmentFormat = l10n.Get("Library.List.TrackCountSegment");
        _webDavSuffixFormat = l10n.Get("Library.List.WebDavSuffix");
        _nasSuffixFormat = l10n.Get("Library.List.NasSuffix");
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(TrackCountSegment));
        OnPropertyChanged(nameof(HoverPath));
    }

    public void UpdateStats(int trackCount, long totalSizeBytes)
    {
        TrackCount = trackCount;
        TotalSizeBytes = totalSizeBytes;
    }

    public void UpdateNasStatus(string? statusSegment, string? statusToolTip, bool isWarning = true)
    {
        NasStatusSegment = statusSegment;
        NasStatusToolTip = string.IsNullOrWhiteSpace(statusToolTip) ? HoverPath : statusToolTip;
        NasStatusUsesWarningBrush = isWarning;
        OnPropertyChanged(nameof(NasStatusSegment));
        OnPropertyChanged(nameof(ShowNasStatusSegment));
        OnPropertyChanged(nameof(ShowNasOnlineStatus));
        OnPropertyChanged(nameof(ShowNasOfflineStatus));
        OnPropertyChanged(nameof(NasStatusToolTip));
        OnPropertyChanged(nameof(NasStatusUsesWarningBrush));
    }

    public string? NasStatusSegment { get; private set; }
    public string? NasStatusToolTip { get; private set; }
    public bool NasStatusUsesWarningBrush { get; private set; }
    public bool ShowNasStatusSegment => IsNetworkSource && !string.IsNullOrWhiteSpace(NasStatusSegment);
    public bool ShowNasOnlineStatus => ShowNasStatusSegment && !NasStatusUsesWarningBrush;
    public bool ShowNasOfflineStatus => ShowNasStatusSegment && NasStatusUsesWarningBrush;

    public string DisabledLabel { get; private set; } = string.Empty;

    public void SetDisabledLabel(string label)
    {
        DisabledLabel = label;
        OnPropertyChanged(nameof(DisabledLabel));
    }

    partial void OnTrackCountChanged(int value)
    {
        OnPropertyChanged(nameof(TrackCountSegment));
    }

    partial void OnTotalSizeBytesChanged(long value)
    {
        OnPropertyChanged(nameof(SizeGbText));
    }

    public LibraryListItemViewModel(MusicLibrary? library, string name, string rootPath, int trackCount, long totalSizeBytes)
    {
        Library = library;
        Name = name;
        RootPath = rootPath;
        _trackCount = trackCount;
        _totalSizeBytes = totalSizeBytes;
    }

    private static string GetLeafFolderName(string rootPath, string fallback)
    {
        if (string.IsNullOrWhiteSpace(rootPath)) return fallback;

        if (WebDavPathHelper.IsWebDavPath(rootPath))
        {
            var trimmed = rootPath.Trim().TrimEnd('/');
            var leaf = WebDavPathHelper.GetFileName(trimmed);
            return string.IsNullOrEmpty(leaf) ? fallback : leaf;
        }

        var trimmedPath = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var folderLeaf = Path.GetFileName(trimmedPath);
        return string.IsNullOrEmpty(folderLeaf) ? fallback : folderLeaf;
    }

    /// <summary>将字节数换算为 GB 字符串（如 142 MB → 0.14 GB）</summary>
    public static string FormatSizeGb(long bytes)
    {
        if (bytes <= 0) return "0 GB";
        var gb = bytes / (1024.0 * 1024.0 * 1024.0);
        return gb >= 100 ? $"{gb:0.#} GB" : $"{gb:0.##} GB";
    }
}
