using MusicVault.Core.Enums;
using MusicVault.Services;
using MusicVault.Services.Download;
using MusicVault.Services.Metadata;
using MusicVault.Services.Search;
using MusicVault.App.Localization;
using MusicVault.App.Services;

namespace MusicVault.Tests;

public sealed class ServiceMessageCodecTests
{
    [Fact]
    public void Resolve_returns_localized_text_for_key()
    {
        var stored = ServiceMessageCodec.Key("Download.IncompleteRetry");
        Assert.Equal("上次未完成，可重试", ServiceMessageCodec.Resolve(stored, "zh-CN"));
        Assert.Equal("Previous download incomplete; retry available", ServiceMessageCodec.Resolve(stored, "en-US"));
    }

    [Fact]
    public void Format_applies_arguments()
    {
        var stored = ServiceMessageCodec.Format("Download.EnrichIncomplete", "disk full");
        Assert.Contains("disk full", ServiceMessageCodec.Resolve(stored, "en-US"));
    }

    [Fact]
    public void Resolve_passthrough_legacy_plain_text()
    {
        const string legacy = "旧版中文错误";
        Assert.Equal(legacy, ServiceMessageCodec.Resolve(legacy, "en-US"));
    }

    [Fact]
    public void Format_nested_platform_key_resolves_in_en_us()
    {
        var stored = ServiceMessageCodec.Format(
            "Search.PlatformTracks",
            ServiceMessageCodec.Key("Platform.NetEase"),
            5);
        var resolved = ServiceMessageCodec.Resolve(stored, "en-US");
        Assert.Equal("NetEase 5 tracks", resolved);
    }

    [Fact]
    public void Format_scrape_partial_failure_resolves_dimension_groups_in_en_us()
    {
        var stored = ServiceMessageCodec.Format(
            "Scrape.PartialFailure",
            $"{ServiceMessageCodec.Key("Scrape.Dimension.Tags")}|{ServiceMessageCodec.Key("Scrape.Dimension.Cover")}",
            ServiceMessageCodec.Key("Scrape.Dimension.Lyrics"));
        var resolved = ServiceMessageCodec.Resolve(stored, "en-US");
        Assert.Equal("Wrote Tags, Cover; could not fetch Lyrics", resolved);
    }

    [Fact]
    public void Resolve_gatherer_probe_available_in_en_us()
    {
        var stored = ServiceMessageCodec.Format("Gatherer.Probe.Available", 200);
        Assert.Equal("Available (200)", ServiceMessageCodec.Resolve(stored, "en-US"));
    }

    [Fact]
    public void Resolve_download_quality_nas_copy_in_en_us()
    {
        var stored = ServiceMessageCodec.Key("Download.Quality.NasCopy");
        Assert.Equal("NAS copy", ServiceMessageCodec.Resolve(stored, "en-US"));
    }

    [Fact]
    public void Resolve_gatherer_display_name_in_en_us()
    {
        var stored = ServiceMessageCodec.Key("Gatherer.Name.UnifiedGoMusicApi");
        Assert.Equal("Unified API (go-music-api)", ServiceMessageCodec.Resolve(stored, "en-US"));
    }

    [Fact]
    public void Resolve_myfree_variant_label_in_en_us()
    {
        var stored = ServiceMessageCodec.Format(
            "Provider.MyFreeMp3.VariantLabel",
            "FLAC",
            ServiceMessageCodec.Key("Provider.MyFreeMp3.Channel.Quark"));
        Assert.Equal("FLAC·Quark", ServiceMessageCodec.Resolve(stored, "en-US"));
    }

    [Fact]
    public void Resolve_artist_source_names_in_en_us()
    {
        Assert.Equal("NetEase Music", ServiceMessageCodec.Resolve(ArtistSourceKeys.NetEaseMusic, "en-US"));
        Assert.Equal("Baidu Baike", ServiceMessageCodec.Resolve(ArtistSourceKeys.BaiduBaike, "en-US"));
    }

    [Fact]
    public void Format_album_description_genre_and_release_in_en_us()
    {
        var genre = ServiceMessageCodec.Format("Album.Description.Genre", "Rock");
        var release = ServiceMessageCodec.Format("Album.Description.Release", "2020");
        Assert.Equal("Genre: Rock", ServiceMessageCodec.Resolve(genre, "en-US"));
        Assert.Equal("Released: 2020", ServiceMessageCodec.Resolve(release, "en-US"));
    }

    [Fact]
    public void Resolve_favorite_playlist_description_in_en_us()
    {
        var stored = ServiceMessageCodec.Key("Favorite.PlaylistDescription");
        Assert.Equal("My favorite music", ServiceMessageCodec.Resolve(stored, "en-US"));
    }

    [Fact]
    public void Format_album_description_release_year_in_en_us()
    {
        var stored = ServiceMessageCodec.Format("Album.Description.ReleaseYear", "2020");
        Assert.Equal("Release year: 2020", ServiceMessageCodec.Resolve(stored, "en-US"));
    }

    [Fact]
    public void Resolve_album_type_labels_in_en_us()
    {
        Assert.Equal("Album", ServiceMessageCodec.Resolve(AlbumTypeKeys.Album, "en-US"));
        Assert.Equal("Single", ServiceMessageCodec.Resolve(AlbumTypeKeys.Single, "en-US"));
    }

    [Fact]
    public void Format_update_and_api_update_messages_in_en_us()
    {
        var updateFailed = ServiceMessageCodec.Format("Update.Failed", "network");
        Assert.Equal("Update failed: network", ServiceMessageCodec.Resolve(updateFailed, "en-US"));

        var apiApplied = ServiceMessageCodec.Format("ApiUpdate.Applied", "v1.0.2");
        Assert.Equal("Updated to v1.0.2; API restarted", ServiceMessageCodec.Resolve(apiApplied, "en-US"));
    }

    [Fact]
    public void Resolve_api_update_progress_stages_in_en_us()
    {
        Assert.Equal("Connecting to GitHub…", ServiceMessageCodec.Resolve(
            ServiceMessageCodec.Key("ApiUpdate.Progress.Connecting"), "en-US"));
        Assert.Equal("Complete", ServiceMessageCodec.Resolve(
            ServiceMessageCodec.Key("ApiUpdate.Progress.Complete"), "en-US"));
    }

    [Fact]
    public void Resolve_playback_diagnostic_messages_in_en_us()
    {
        Assert.Equal(
            "Skipped: track missing or invalid path",
            ServiceMessageCodec.Resolve(ServiceMessageCodec.Key("PlaybackDiag.RestoreSkipInvalid"), "en-US"));

        var restoreError = ServiceMessageCodec.Format("PlaybackDiag.RestoreError", "timeout");
        Assert.Equal("Session restore error: timeout", ServiceMessageCodec.Resolve(restoreError, "en-US"));

        var playOk = ServiceMessageCodec.Format("PlaybackDiag.PlayOk", "Song", @"C:\music\song.flac");
        Assert.Equal("Play OK: Song @ C:\\music\\song.flac", ServiceMessageCodec.Resolve(playOk, "en-US"));

        var restoreUi = ServiceMessageCodec.Format("PlaybackDiag.RestoreUi", "Song", "42", 3);
        Assert.Equal("Restore UI: Song pos=42s history=3", ServiceMessageCodec.Resolve(restoreUi, "en-US"));
    }

    [Fact]
    public void EqualizerPresets_normalize_legacy_custom_ids()
    {
        Assert.Equal(EqualizerPresets.Custom, EqualizerPresets.NormalizePresetId("自定义"));
        Assert.Equal(EqualizerPresets.DefaultCustomPresetStorageKey,
            EqualizerPresets.NormalizeCustomPresetStorageName("我的预设"));
    }

    [Fact]
    public async Task EqualizerPresetLocalizer_builtin_presets_localize()
    {
        var dir = Path.Combine(Path.GetTempPath(), "MusicVault.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var settings = new AppSettingsService(dir);
        var l10n = new LocalizationService(settings);

        Assert.Equal("古典", EqualizerPresetLocalizer.Format(l10n, "Classical"));

        await l10n.SetCultureAsync(LocalizationService.EnUs);
        Assert.Equal("Classical", EqualizerPresetLocalizer.Format(l10n, "Classical"));
        Assert.Equal("Custom", EqualizerPresetLocalizer.Format(l10n, EqualizerPresets.Custom));
    }

    [Fact]
    public void Resolve_api_search_and_health_messages_in_en_us()
    {
        Assert.Equal("Checking music API…", ServiceMessageCodec.Resolve(
            ServiceMessageCodec.Key("Api.Search.Checking"), "en-US"));
        Assert.Equal(
            "Bundled music API failed to start; trying direct platform search",
            ServiceMessageCodec.Resolve(ServiceMessageCodec.Key("Api.Search.BundledFallback"), "en-US"));
        Assert.Equal("Probe timed out", ServiceMessageCodec.Resolve(
            ServiceMessageCodec.Key("Api.Probe.Timeout"), "en-US"));
        Assert.Equal("Connected", ServiceMessageCodec.Resolve(
            ServiceMessageCodec.Key("Api.Connection.Ok"), "en-US"));
    }

    [Fact]
    public void Resolve_download_quality_chip_and_describe_in_en_us()
    {
        Assert.Equal("128K", ServiceMessageCodec.Resolve(
            DownloadQualityLabels.Chip(AudioQuality.Standard128), "en-US"));
        Assert.Equal("320K", ServiceMessageCodec.Resolve(
            DownloadQualityLabels.Chip(AudioQuality.High320), "en-US"));
        Assert.Equal("FLAC · .flac", ServiceMessageCodec.Resolve(
            DownloadQualityLabels.Describe(AudioQuality.Flac), "en-US"));
        Assert.Equal("MP3 320 · .mp3", ServiceMessageCodec.Resolve(
            DownloadQualityLabels.Describe(AudioQuality.High320), "en-US"));
        Assert.Equal(
            "Quality priority (FLAC → 320K → 128K)",
            ServiceMessageCodec.Resolve(ServiceMessageCodec.Key("Download.Quality.PriorityChain"), "en-US"));
        Assert.Equal(
            "Library albums",
            ServiceMessageCodec.Resolve(ServiceMessageCodec.Key("Album.Source.LibraryAggregated"), "en-US"));
        Assert.Equal(
            "4.3 MB",
            ServiceMessageCodec.Resolve(
                OnlineTrackDisplaySize.FormatOrDash(4_500_000),
                "en-US"));
        Assert.Equal(
            "non-FLAC format",
            ServiceMessageCodec.Resolve(ServiceMessageCodec.Key("Download.FormatNotFlac"), "en-US"));
    }

    [Fact]
    public void BatchScrapeMessageFormatter_maps_legacy_english_and_chinese()
    {
        Assert.Equal(
            "Scrape incomplete",
            ServiceMessageCodec.Resolve(
                BatchScrapeMessageFormatter.FormatTrackResult("Scrape incomplete"),
                "en-US"));
        Assert.Equal(
            "No usable online song match found",
            ServiceMessageCodec.Resolve(
                BatchScrapeMessageFormatter.FormatTrackResult("No usable online song match found"),
                "en-US"));
        Assert.Equal(
            "Could not write Tags",
            ServiceMessageCodec.Resolve(
                BatchScrapeMessageFormatter.FormatTrackResult("Tags未获取"),
                "en-US"));
    }

    [Fact]
    public void ResolveDetailAggregate_resolves_joined_service_messages()
    {
        var settings = new AppSettingsService(Path.Combine(Path.GetTempPath(), "MusicVault.Tests", Guid.NewGuid().ToString("N")));
        var l10n = new LocalizationService(settings);
        var joined = string.Join(
            " · ",
            ServiceMessageCodec.Key("Api.Connection.Ok"),
            ServiceMessageCodec.Key("Api.HealthCheck.NoResponse"));
        var resolved = ServiceMessageLocalizer.ResolveDetailAggregate(l10n, joined);
        Assert.Equal("连通正常 · API 健康检查无响应", resolved);
    }
}
