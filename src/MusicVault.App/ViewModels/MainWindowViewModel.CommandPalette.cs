using CommunityToolkit.Mvvm.Input;
using MusicVault.App.ViewModels.Pages;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;

namespace MusicVault.App.ViewModels;

public partial class MainWindowViewModel
{
    [RelayCommand]
    private void ToggleCommandPalette()
    {
        if (IsCompactPlayerMode)
            return;

        if (CommandPalette.IsOpen)
            CommandPalette.Close();
        else
            CommandPalette.Open();
    }

    private IReadOnlyList<CommandPaletteEntry> BuildCommandPaletteEntries()
    {
        var groupNav = _l10n.Get("CommandPalette.Group.Navigate");
        var groupSettings = _l10n.Get("CommandPalette.Group.Settings");
        var groupPlayback = _l10n.Get("CommandPalette.Group.Playback");
        var groupActions = _l10n.Get("CommandPalette.Group.Actions");

        var items = new List<CommandPaletteEntry>();

        void AddNav(string titleKey, NavigationSection section, params string[] keywords)
        {
            items.Add(new CommandPaletteEntry(
                _l10n.Get(titleKey),
                null,
                groupNav,
                keywords,
                () => NavigateFromPalette(section)));
        }

        AddNav("Nav.Home", NavigationSection.Home, "home", "首页", "shouye");
        AddNav("Nav.Discover", NavigationSection.Search, "discover", "search", "发现", "在线", "搜索");
        AddNav("Nav.Transfers", NavigationSection.Downloads, "download", "transfer", "下载", "传输");
        AddNav("Nav.Browse.Tracks", NavigationSection.AllTracks, "tracks", "songs", "曲目", "歌曲", "曲库");
        AddNav("Nav.Browse.Albums", NavigationSection.Albums, "album", "专辑");
        AddNav("Nav.Browse.Artists", NavigationSection.Artists, "artist", "歌手", "艺术家");
        AddNav("Nav.Browse.Playlists", NavigationSection.Playlists, "playlist", "歌单");
        AddNav("Nav.Library", NavigationSection.Libraries, "library", "nas", "webdav", "音乐库", "库");
        AddNav("Nav.Tools", NavigationSection.AudioTools, "tools", "organize", "整理", "刮削", "工具");
        AddNav("Nav.Settings", NavigationSection.Settings, "settings", "设置", "偏好");

        void AddSettings(string titleKey, SettingsSectionKind section, params string[] keywords)
        {
            items.Add(new CommandPaletteEntry(
                _l10n.Get(titleKey),
                null,
                groupSettings,
                keywords,
                () => NavigateSettingsSection(section)));
        }

        AddSettings("Settings.Section.Overview", SettingsSectionKind.Overview, "overview", "概览");
        AddSettings("Settings.Section.Appearance", SettingsSectionKind.Appearance, "appearance", "theme", "外观", "主题");
        AddSettings("Settings.Section.Playback", SettingsSectionKind.PlaybackAudio, "playback", "audio", "播放", "歌词", "快捷键");
        AddSettings("Settings.Section.DownloadCache", SettingsSectionKind.DownloadCache, "download", "cache", "下载", "缓存");
        AddSettings("Settings.Section.DataBackup", SettingsSectionKind.DataBackup, "data", "backup", "数据", "备份");
        AddSettings("Settings.Section.Advanced", SettingsSectionKind.Advanced, "api", "online", "在线源", "api");
        AddSettings("Settings.Section.UpdateDiagnostics", SettingsSectionKind.UpdateDiagnostics, "update", "diagnostics", "更新", "诊断");

        items.Add(new CommandPaletteEntry(
            _l10n.Get("Settings.Playback.LibVlcEngine"),
            _l10n.Get("CommandPalette.Action.OpenLibVlcSetting.Subtitle"),
            groupSettings,
            ["libvlc", "vlc", "engine", "引擎", "实验", "解码", "ape", "wma"],
            () => OpenLibVlcEngineSettingFromPalette()));

        items.Add(new CommandPaletteEntry(
            _l10n.Get("Settings.Shortcut.PlayPause"),
            null,
            groupPlayback,
            ["play", "pause", "space", "播放", "暂停"],
            () => Player.TogglePlayPauseCommand.Execute(null),
            () => Player.HasActivePlayback));

        items.Add(new CommandPaletteEntry(
            _l10n.Get("Settings.Shortcut.Next"),
            null,
            groupPlayback,
            ["next", "skip", "下一首"],
            () => Player.NextCommand.Execute(null),
            () => Player.HasActivePlayback));

        items.Add(new CommandPaletteEntry(
            _l10n.Get("Settings.Shortcut.Previous"),
            null,
            groupPlayback,
            ["previous", "back", "上一首"],
            () => Player.PreviousCommand.Execute(null),
            () => Player.HasActivePlayback));

        items.Add(new CommandPaletteEntry(
            _l10n.Get("Settings.Shortcut.Lyrics"),
            null,
            groupPlayback,
            ["lyrics", "歌词", "ctrl+l"],
            () => Player.ToggleLyricsCommand.Execute(null),
            () => Player.CanToggleLyrics));

        items.Add(new CommandPaletteEntry(
            _l10n.Get("Player.Fullscreen"),
            null,
            groupPlayback,
            ["fullscreen", "now playing", "全屏", "正在播放"],
            () => Player.OpenNowPlayingFullscreenCommand.Execute(null),
            () => Player.CanOpenNowPlayingFullscreen));

        items.Add(new CommandPaletteEntry(
            _l10n.Get("Player.Compact.Enter"),
            null,
            groupPlayback,
            ["compact", "mini", "精简", "小窗"],
            () => ToggleCompactPlayerCommand.Execute(null),
            () => !IsCompactPlayerMode && Player.HasActivePlayback));

        items.Add(new CommandPaletteEntry(
            _l10n.Get("Library.Favorites"),
            _l10n.Get("CommandPalette.Action.OpenFavorites.Subtitle"),
            groupActions,
            ["favorite", "liked", "收藏", "喜欢", "favorites"],
            () => OpenFavoritesFromPalette()));

        items.Add(new CommandPaletteEntry(
            _l10n.Get("CommandPalette.Action.SearchTracks"),
            _l10n.Get("CommandPalette.Action.SearchTracks.Subtitle"),
            groupActions,
            ["search", "find", "local", "搜索", "曲目", "ctrl+f"],
            () => FocusLocalSearchCommand.Execute(null)));

        items.Add(new CommandPaletteEntry(
            _l10n.Get("CommandPalette.Action.CycleTheme"),
            _l10n.Get("CommandPalette.Action.CycleTheme.Subtitle"),
            groupActions,
            ["theme", "dark", "light", "主题", "深色", "浅色"],
            () => _ = Theme.CycleThemeAsync()));

        return items;
    }

    private async Task<IReadOnlyList<CommandPaletteEntry>> LoadCommandPaletteRecentEntriesAsync(
        CancellationToken ct)
    {
        const int limit = 8;
        var group = _l10n.Get("CommandPalette.Group.Recent");
        var entries = new List<CommandPaletteEntry>();
        var seenIds = new HashSet<long>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var track in Player.Queue.Reverse())
        {
            if (!TryAddCommandPaletteTrackEntry(entries, track, group, seenIds, seenPaths))
                continue;
            if (entries.Count >= limit)
                return entries;
        }

        if (entries.Count < limit)
        {
            var dbTracks = await _services.Tracks
                .GetRecentlyPlayedAsync(limit - entries.Count, seenIds, ct)
                .ConfigureAwait(true);
            foreach (var track in dbTracks)
            {
                if (TryAddCommandPaletteTrackEntry(entries, track, group, seenIds, seenPaths)
                    && entries.Count >= limit)
                    break;
            }
        }

        return entries;
    }

    private async Task<IReadOnlyList<CommandPaletteEntry>> LoadCommandPaletteFavoriteEntriesAsync(
        CancellationToken ct)
    {
        const int limit = 8;
        var playlistId = await _services.Favorites.GetPlaylistIdAsync(ct).ConfigureAwait(true);
        var tracks = await _services.Tracks.GetByPlaylistAsync(playlistId, 0, limit, ct)
            .ConfigureAwait(true);
        if (tracks.Count == 0)
            return [];

        var group = _l10n.Get("CommandPalette.Group.Favorites");
        return tracks.Select(track => CreateCommandPaletteTrackEntry(track, group)).ToList();
    }

    private void OpenFavoritesFromPalette()
    {
        AllTracks.FavoritesOnly = true;
        NavigateFromPalette(NavigationSection.AllTracks);
    }

    private bool TryAddCommandPaletteTrackEntry(
        List<CommandPaletteEntry> target,
        Track track,
        string group,
        HashSet<long> seenIds,
        HashSet<string> seenPaths)
    {
        if (track.Id > 0)
        {
            if (!seenIds.Add(track.Id))
                return false;
        }
        else if (!string.IsNullOrWhiteSpace(track.FilePath))
        {
            if (!seenPaths.Add(track.FilePath))
                return false;
        }
        else
        {
            return false;
        }

        target.Add(CreateCommandPaletteTrackEntry(track, group));
        return true;
    }

    private async Task<IReadOnlyList<CommandPaletteEntry>> SearchCommandPaletteTracksAsync(
        string query,
        CancellationToken ct)
    {
        var trimmed = query.Trim();
        if (trimmed.Length < 2)
            return [];

        var tracks = await _services.Tracks.SearchScopedAsync(trimmed, null, null, 0, 12, ct)
            .ConfigureAwait(true);
        if (tracks.Count == 0)
            return [];

        var group = _l10n.Get("CommandPalette.Group.Tracks");
        return tracks
            .Select(track => CreateCommandPaletteTrackEntry(track, group))
            .ToList();
    }

    private async Task<IReadOnlyList<CommandPaletteEntry>> SearchCommandPaletteFavoriteTracksAsync(
        string query,
        CancellationToken ct)
    {
        var playlistId = await _services.Favorites.GetPlaylistIdAsync(ct).ConfigureAwait(true);
        var trimmed = query.Trim();
        IReadOnlyList<Track> tracks;
        if (trimmed.Length == 0)
        {
            tracks = await _services.Tracks.GetByPlaylistAsync(playlistId, 0, 12, ct)
                .ConfigureAwait(true);
        }
        else
        {
            tracks = await _services.Tracks.SearchScopedAsync(trimmed, null, playlistId, 0, 12, ct)
                .ConfigureAwait(true);
        }

        if (tracks.Count == 0)
            return [];

        var group = _l10n.Get("CommandPalette.Group.Favorites");
        return tracks
            .Select(track => CreateCommandPaletteTrackEntry(track, group))
            .ToList();
    }

    private CommandPaletteEntry CreateCommandPaletteTrackEntry(Track track, string group)
    {
        var captured = track;
        var sourceLabel = group;
        return new CommandPaletteEntry(
            track.Title,
            FormatCommandPaletteTrackSubtitle(track),
            group,
            [track.Artist, track.Album, track.FilePath],
            () => _ = PlayTrackFromCommandPaletteAsync(captured, sourceLabel));
    }

    private static string FormatCommandPaletteTrackSubtitle(Track track)
    {
        if (string.IsNullOrWhiteSpace(track.Artist))
            return track.Album;
        if (string.IsNullOrWhiteSpace(track.Album))
            return track.Artist;
        return $"{track.Artist} · {track.Album}";
    }

    private async Task PlayTrackFromCommandPaletteAsync(Track track, string queueSourceLabel)
    {
        await Player.PlayTrackAsync(
                track,
                queueSourceLabel: queueSourceLabel)
            .ConfigureAwait(true);
    }

    private void NavigateFromPalette(NavigationSection section)
    {
        NavigateTo(section);
        _ = InitializePageAsync(section);
    }

    private void NavigateSettingsSection(SettingsSectionKind section)
    {
        Settings.SelectedSection = section;
        NavigateFromPalette(NavigationSection.Settings);
    }

    private void OpenLibVlcEngineSettingFromPalette()
    {
        Settings.SelectedSection = SettingsSectionKind.PlaybackAudio;
        Settings.SelectedPlaybackAudioTab = PlaybackAudioTabKind.Playback;
        NavigateFromPalette(NavigationSection.Settings);
    }
}
