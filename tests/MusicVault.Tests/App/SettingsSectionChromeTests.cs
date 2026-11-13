using System.Text.RegularExpressions;

namespace MusicVault.Tests.App;

/// <summary>设置二级页 chrome 合同：共用页头、偏好行 108,12,*（固定标签列）。</summary>
public sealed class SettingsSectionChromeTests
{
    private const string PreferenceFieldColumns = "108,12,*";

    private static readonly Regex FieldRowColumns = new(
        @"<Grid\s+Classes=""settings-field-row[^""]*""[^>]*ColumnDefinitions=""([^""]+)""",
        RegexOptions.Compiled);

    [Fact]
    public void Settings_page_has_single_shared_header()
    {
        var axaml = ReadSettingsPage();
        var headers = Regex.Matches(axaml, @"Classes=""settings-page-header""");
        Assert.Single(headers);
        Assert.Contains("AppearanceInstantHint", axaml);
        Assert.DoesNotContain(
            "Classes=\"settings-page-header\"",
            ExtractSection(axaml, "<!-- ====== Appearance ====== -->", "<!-- ====== PlaybackAudio ====== -->"));
        Assert.DoesNotContain(
            "Classes=\"settings-page-header\"",
            ExtractSection(axaml, "<!-- ====== PlaybackAudio ====== -->", "<!-- ====== DownloadCache ====== -->"));
        Assert.Contains("ShowLibraryStatsHeaderActions", axaml);
    }

    [Fact]
    public void Settings_field_rows_use_contract_columns()
    {
        var axaml = ReadSettingsPage();
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            PreferenceFieldColumns,
        };

        var violations = FieldRowColumns.Matches(axaml)
            .Cast<Match>()
            .Select(m => m.Groups[1].Value)
            .Where(cols => !allowed.Contains(cols))
            .Distinct()
            .ToList();

        Assert.True(violations.Count == 0, "unexpected field-row columns: " + string.Join("; ", violations));
        Assert.Contains(PreferenceFieldColumns, FieldRowColumns.Matches(axaml).Select(m => m.Groups[1].Value));
    }

    [Fact]
    public void Preference_field_labels_are_right_aligned_without_metadata_class()
    {
        var axaml = ReadSettingsPage();
        var fieldLabelHits = Regex.Matches(
            axaml,
            @"Classes=""settings-field-row[^""]*""[\s\S]*?Classes=""([^""]*settings-field-label[^""]*)""");
        Assert.True(fieldLabelHits.Count > 0);
        foreach (Match hit in fieldLabelHits)
        {
            Assert.DoesNotContain("metadata-field-label", hit.Groups[1].Value);
            Assert.DoesNotContain("text-label", hit.Groups[1].Value);
        }

        var styles = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "src", "MusicVault.App", "Styles", "Components", "Settings.axaml"));
        Assert.Contains("Grid.settings-field-row > TextBlock.settings-field-label", styles);
        Assert.Matches(
            @"Grid\.settings-field-row\s*>\s*TextBlock\.settings-field-label[\s\S]*?Property=""FontSize""\s+Value=""\{DynamicResource FontSizeBody\}""",
            styles);
        Assert.Contains(
            """
  <Style Selector="Grid.settings-field-row">
    <Setter Property="HorizontalAlignment" Value="Stretch"/>
    <Setter Property="MinHeight" Value="36"/>
    <Setter Property="Margin" Value="0"/>
  </Style>
""",
            styles.Replace("\r\n", "\n"));

        var blocks = Regex.Matches(
            styles,
            @"<Style Selector=""([^""]+)"">\s*(.*?)\s*</Style>",
            RegexOptions.Singleline);
        var lastAlignment = "";
        foreach (Match block in blocks)
        {
            var selector = block.Groups[1].Value;
            if (!selector.Contains("settings-field-row", StringComparison.Ordinal))
                continue;
            if (selector.Contains("usage-kv", StringComparison.Ordinal))
                continue;
            if (!selector.Contains("settings-field-label", StringComparison.Ordinal))
                continue;
            var align = Regex.Match(block.Groups[2].Value, @"Property=""TextAlignment""\s+Value=""([^""]+)""");
            if (align.Success)
                lastAlignment = align.Groups[1].Value;
        }

        Assert.Equal("Right", lastAlignment);
    }

    [Fact]
    public void Appearance_page_is_display_then_accent_with_inline_reset()
    {
        var appearance = ExtractSection(
            ReadSettingsPage(),
            "<!-- ====== Appearance ====== -->",
            "<!-- ====== PlaybackAudio ====== -->");
        var display = appearance.IndexOf("AppearanceDisplayGroupTitle", StringComparison.Ordinal);
        var language = appearance.IndexOf("AppearanceLanguageLabel", StringComparison.Ordinal);
        var theme = appearance.IndexOf("AppearanceThemeLabel", StringComparison.Ordinal);
        var font = appearance.IndexOf("AppearanceFontScaleLabel", StringComparison.Ordinal);
        var accent = appearance.IndexOf("AppearanceAccentLabel", StringComparison.Ordinal);
        var swatch = appearance.IndexOf("settings-appearance-swatch-list", StringComparison.Ordinal);
        var reset = appearance.IndexOf("ResetAppearanceToDefaultsCommand", StringComparison.Ordinal);
        Assert.True(display >= 0 && language > display && theme > language && font > theme);
        Assert.True(accent > font && swatch > accent && reset > swatch);
        Assert.Contains("settings-field-follow-action", appearance);
        Assert.DoesNotContain("Text=\"{Binding AppearanceResetDefaultLabel}\"", appearance);
        Assert.DoesNotContain("settings-appearance-swatch-well", appearance);
        Assert.DoesNotContain("AppearanceResetGroupTitle", appearance);
        Assert.DoesNotContain("settings-group-title\" ToolTip.Tip=\"{Binding AppearanceAccentHint}\"", appearance);

        var styles = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "src", "MusicVault.App", "Styles", "Components", "Settings.axaml"));
        Assert.Contains("ItemsControl.settings-appearance-swatch-list", styles);
        Assert.DoesNotContain("Border.settings-appearance-swatch-well", styles);
    }

    [Fact]
    public void Playback_tab_content_groups_defaults_engine_capability()
    {
        var playback = ExtractSection(
            ReadSettingsPage(),
            "<!-- ====== PlaybackAudio",
            "<!-- ====== DownloadCache");
        Assert.Contains("AntdCardTabContentMargin", playback);
        Assert.Contains("settings-playback-tab-content", playback);
        Assert.Contains("PlaybackDefaultsGroupTitle", playback);
        Assert.Contains("PlaybackEngineGroupTitle", playback);
        Assert.Contains("PlaybackCapabilityGroupTitle", playback);
        Assert.Contains("PlatformCapabilityTitle", playback);
        Assert.Contains("ActivePlaybackEngineName", playback);
        Assert.Contains("ToolTip.Tip=\"{Binding ActivePlaybackEngineDetail}\"", playback);
        Assert.DoesNotContain("ShowActivePlaybackEngineDetail", playback);
        Assert.Contains("settings-field-follow-caption", playback);
        Assert.Contains("PlaybackShortcutsEnableLabel", playback);
        Assert.Contains("PlaybackShortcutsManageLabel", playback);
        Assert.Contains("DesktopLyricsUnsupportedHint", playback);
        Assert.DoesNotContain("settings-shortcut-actions", playback);

        var defaults = playback.IndexOf("PlaybackDefaultsGroupTitle", StringComparison.Ordinal);
        var engine = playback.IndexOf("PlaybackEngineGroupTitle", StringComparison.Ordinal);
        var capability = playback.IndexOf("PlaybackCapabilityGroupTitle", StringComparison.Ordinal);
        var restore = playback.IndexOf("PlaybackRestoreOnStartupLabel", StringComparison.Ordinal);
        var quality = playback.IndexOf("PlaybackDefaultQualityLabel", StringComparison.Ordinal);
        Assert.True(defaults >= 0 && restore > defaults && engine > restore && capability > engine && quality > capability);
    }

    [Fact]
    public void Nas_cache_fields_live_on_download_not_playback()
    {
        var axaml = ReadSettingsPage();
        var playback = ExtractSection(axaml, "<!-- ====== PlaybackAudio", "<!-- ====== DownloadCache");
        var download = ExtractSection(axaml, "<!-- ====== DownloadCache", "<!-- ====== DataBackup");
        Assert.DoesNotContain("NasCacheOnPlayLabel", playback);
        Assert.DoesNotContain("NasDownloadTargetLibraryLabel", playback);
        Assert.DoesNotContain("NasPlaybackCacheStatsLabel", playback);
        var files = download.IndexOf("DownloadFilesGroupTitle", StringComparison.Ordinal);
        var nas = download.IndexOf("NasCacheOnPlayLabel", StringComparison.Ordinal);
        var quality = download.IndexOf("PlaybackDefaultQualityLabel", StringComparison.Ordinal);
        Assert.True(files >= 0 && quality > files && nas > quality);
        Assert.Contains("DownloadCacheGroupTitle", download);
        Assert.DoesNotContain("PersistSettingsFooterHint", download);
        Assert.Contains("settings-download-page", download);
        Assert.Contains("settings-inline-control-compact", download);
        Assert.Contains("DownloadClearCacheLabel", download);
        var stats = download.IndexOf("NasPlaybackCacheStatsLabel", StringComparison.Ordinal);
        var clear = download.IndexOf("DownloadClearCacheLabel", StringComparison.Ordinal);
        Assert.True(stats >= 0 && clear > stats);
    }

    [Fact]
    public void Overview_uses_about_book_identity_about_then_shortcuts()
    {
        var overview = ExtractSection(
            ReadSettingsPage(),
            "<!-- ====== Overview",
            "<!-- ====== Appearance");
        var identity = overview.IndexOf("settings-overview-identity", StringComparison.Ordinal);
        var tagline = overview.IndexOf("IntroductionTagline", StringComparison.Ordinal);
        var about = overview.IndexOf("OverviewAboutSectionTitle", StringComparison.Ordinal);
        var license = overview.IndexOf("OverviewLicenseSectionTitle", StringComparison.Ordinal);
        var shortcuts = overview.IndexOf("OverviewShortcutsTitle", StringComparison.Ordinal);
        var appearance = overview.IndexOf("GoToOverviewAppearanceCommand", StringComparison.Ordinal);
        Assert.True(identity >= 0 && tagline > identity && about > tagline && license > about);
        Assert.True(shortcuts > license && appearance > shortcuts);
        Assert.Contains("OverviewCapabilitiesSummary", overview);
        Assert.Contains("settings-overview-about-body", overview);
        // KPI / 功能 Tab 已迁出概览（KPI → 数据页）
        Assert.DoesNotContain("settings-overview-kpi-card", overview);
        Assert.DoesNotContain("antd-card-tabs", overview);
        Assert.DoesNotContain("IsOverviewAboutExpanded", overview);
        Assert.DoesNotContain("OverviewFeaturesTitle", overview);
        Assert.DoesNotContain("settings-field-row", overview);
    }

    [Fact]
    public void Data_page_hosts_library_kpi_cards()
    {
        var data = ExtractSection(
            ReadSettingsPage(),
            "<!-- ====== DataBackup",
            "<!-- ====== OnlineSources");
        Assert.Contains("OverviewLibraryStatsTitle", data);
        Assert.Contains("settings-overview-kpi-card", data);
        Assert.Contains("ShowLibraryStatsHeaderActions", ReadSettingsPage());
    }

    [Fact]
    public void Settings_preference_combos_use_compact_width_token()
    {
        var spacing = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "src", "MusicVault.App", "Styles", "Tokens", "Spacing.axaml"));
        Assert.Contains("x:Key=\"SettingsComboWidth\"", spacing);
        Assert.Contains(">168<", spacing.Replace(" ", ""));

        var styles = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "src", "MusicVault.App", "Styles", "Components", "Settings.axaml"));
        Assert.Contains("SettingsComboWidth", styles);
        Assert.Contains("settings-field-control-wide", styles);

        var axaml = ReadSettingsPage();
        Assert.Contains("settings-field-control-wide", axaml);
    }

    [Fact]
    public void Data_page_keeps_backup_primary_and_acoustid_secondary()
    {
        var data = ExtractSection(
            ReadSettingsPage(),
            "<!-- ====== DataBackup",
            "<!-- ====== OnlineSources");
        Assert.Contains("settings-data-page", data);
        Assert.Contains("BackupSectionTitle", data);
        Assert.Contains("CreateBackupCommand", data);
        Assert.Contains("OpenBackupFolderCommand", data);
        Assert.DoesNotContain("BackupExportDiagnosticsLabel", data);
        Assert.DoesNotContain("ExportDiagnosticsCommand", data);
        Assert.DoesNotContain("settings-section-divider", data);
        Assert.Contains("flat-btn btn-toolbar settings-backup-restore-action", data);
        Assert.DoesNotContain("primary btn-toolbar settings-backup-restore-action", data);
        var backup = data.IndexOf("BackupSectionTitle", StringComparison.Ordinal);
        var acoustId = data.IndexOf("AcoustIdLookupTitle", StringComparison.Ordinal);
        var enable = data.IndexOf("AcoustIdEnableLabel", StringComparison.Ordinal);
        var testRow = data.IndexOf("AcoustIdTestRowLabel", StringComparison.Ordinal);
        Assert.True(backup >= 0 && acoustId > backup && enable > acoustId && testRow > enable);
    }

    [Fact]
    public void Online_page_orders_status_then_bundled_then_advanced_expanders()
    {
        var online = ExtractSection(
            ReadSettingsPage(),
            "<!-- ====== OnlineSources",
            "<!-- ====== UpdateDiagnostics");
        Assert.Contains("settings-online-page", online);
        Assert.DoesNotContain("ShowOnlineSourcesPersistHint", online);
        Assert.DoesNotContain("SettingsPersistHint", online);
        Assert.Contains("settings-online-status-summary", online);
        Assert.Contains("settings-online-switch-row", online);
        Assert.Contains("settings-online-advanced-section", online);
        Assert.Contains("settings-online-disclosure", online);
        Assert.Contains("settings-online-disclosure-header", online);
        Assert.Contains("settings-connection-test-primary", online);
        Assert.Contains("settings-connection-test-secondary", online);
        Assert.Contains("OnlineLoginStatusLabel", online);
        Assert.Contains("OnlineAdvancedSectionTitle", online);
        Assert.DoesNotContain("<Expander", online);
        Assert.DoesNotContain("ant-collapse", online);
        Assert.Contains("IsPlatformApisExpanded", online);
        Assert.Contains("IsRecognitionExpanded", online);
        Assert.Contains("IsGatherersExpanded", online);
        Assert.Contains("PlatformDisclosureCaption", online);
        Assert.DoesNotContain("settings-connection-more-line", online);

        var connection = online.IndexOf("OnlineConnectionHealthTitle", StringComparison.Ordinal);
        var bundled = online.IndexOf("OnlineBundledServicesTitle", StringComparison.Ordinal);
        var platform = online.IndexOf("OnlinePlatformApisTitle", StringComparison.Ordinal);
        var recognition = online.IndexOf("OnlineRecognitionGroupTitle", StringComparison.Ordinal);
        var gatherers = online.IndexOf("OnlineGathererSourcesTitle", StringComparison.Ordinal);
        var disclosure = online.IndexOf("settings-online-disclosure", StringComparison.Ordinal);
        Assert.True(connection >= 0 && bundled > connection);
        Assert.True(disclosure > bundled);
        Assert.True(platform > disclosure && recognition > platform && gatherers > recognition);
        Assert.Contains("GathererItems", online);
        Assert.Contains("settings-gatherer-row", online);
        Assert.Contains("UniformGrid", online);
        Assert.Contains("SettingsGathererColumns", online);
        Assert.Contains("ShowPlatformNeedsAttentionBadge", online);
        Assert.Contains("OnlineTestAllGatherersLabel", online);

        var spacing = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "src", "MusicVault.App", "Styles", "Tokens", "Spacing.axaml"));
        Assert.Contains("SettingsGathererListWidth", spacing);
        Assert.Contains("x:Key=\"SettingsGathererColumns\"", spacing);

        var styles = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "src", "MusicVault.App", "Styles", "Components", "Settings.axaml"));
        Assert.Contains("TextBlock.settings-gatherer-name", styles);
        Assert.Matches(
            @"TextBlock\.settings-gatherer-name[\s\S]*?Property=""TextAlignment""\s+Value=""Right""",
            styles);
    }

    [Fact]
    public void Update_page_groups_app_feed_disclosure_and_light_troubleshoot()
    {
        var axaml = ReadSettingsPage();
        var start = axaml.IndexOf("<!-- ====== UpdateDiagnostics", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var update = axaml[start..];
        Assert.DoesNotContain("UpdateDiagnosticsPersistHint", update);
        Assert.DoesNotContain("settings-troubleshoot-panel", update);
        Assert.Contains("UpdateAppGroupTitle", update);
        Assert.Contains("UpdatePreferencesGroupTitle", update);
        Assert.Contains("IsUpdateFeedExpanded", update);
        Assert.Contains("UpdateFeedDisclosureCaption", update);
        Assert.Contains("UpdateBundledGroupTitle", update);
        Assert.Contains("DiagnosticsToolsGroupTitle", update);
        Assert.Contains("IsUsageInsightsExpanded", update);
        Assert.Contains("HasPendingUpdate", update);
        Assert.Contains("UpdateRestartInstallLabel", update);
        var appTab = update.IndexOf("settings-update-app-tab", StringComparison.Ordinal);
        var feed = update.IndexOf("IsUpdateFeedExpanded", StringComparison.Ordinal);
        var freq = update.IndexOf("UpdateFrequencyFieldLabel", StringComparison.Ordinal);
        Assert.True(appTab >= 0 && freq > appTab && feed > freq);
    }

    [Fact]
    public void Form_pages_share_section_body_spacing_and_disclosure_alias()
    {
        var axaml = ReadSettingsPage();
        Assert.Contains("settings-section-body settings-appearance-body", axaml);
        Assert.Contains("settings-section-body settings-download-body", axaml);
        Assert.Contains("settings-section-body settings-data-body", axaml);
        Assert.Contains("settings-section-body settings-online-body", axaml);
        Assert.Contains("settings-section-body settings-playback-tab-body", axaml);
        Assert.Contains("settings-section-body settings-update-app-tab", axaml);
        Assert.Contains("settings-section-body settings-update-bundled-tab", axaml);
        Assert.Contains("settings-section-body settings-update-troubleshoot-tab", axaml);
        Assert.DoesNotContain("settings-playback-tab-body\" Spacing=\"16\"", axaml);
        Assert.DoesNotContain("settings-update-app-tab\" Spacing=\"16\"", axaml);

        var spacing = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "src", "MusicVault.App", "Styles", "Tokens", "Spacing.axaml"));
        Assert.Contains("x:Key=\"SettingsSectionBodySpacing\"", spacing);

        var styles = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "src", "MusicVault.App", "Styles", "Components", "Settings.axaml"));
        Assert.Contains("SettingsSectionBodySpacing", styles);
        Assert.Contains("StackPanel.settings-disclosure", styles);
        Assert.Contains("ToggleButton.settings-disclosure-header", styles);
        Assert.Contains("StackPanel.settings-disclosure-body", styles);

        Assert.Contains("settings-online-disclosure", axaml);
        var feed = axaml.IndexOf("IsUpdateFeedExpanded", StringComparison.Ordinal);
        var platform = axaml.IndexOf("IsPlatformApisExpanded", StringComparison.Ordinal);
        Assert.True(feed >= 0 && platform >= 0);
    }

    [Fact]
    public void Settings_has_no_explicit_save_chrome()
    {
        var axaml = ReadSettingsPage();
        Assert.DoesNotContain("settings-sticky-save-bar", axaml);
        Assert.DoesNotContain("settings-save-header-btn", axaml);
        Assert.DoesNotContain("SaveSettingsCommand", axaml);
        Assert.DoesNotContain("ShowHeaderSaveButton", axaml);
        Assert.DoesNotContain("ShowStickySaveBar", axaml);

        var styles = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "src", "MusicVault.App", "Services", "SettingsAutosaveScheduler.cs"));
        Assert.Contains("DebounceMilliseconds = 400", styles);
        Assert.Contains("SettingsAutosaveKind", styles);
    }

    [Fact]
    public void Section_page_header_is_shown_for_overview()
    {
        var vm = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "MusicVault.App", "ViewModels", "Pages", "SettingsPageViewModel.cs"));
        Assert.Contains("public bool ShowSectionPageHeader => true;", vm);
        Assert.Contains("ShowLibraryStatsHeaderActions", vm);
    }

    private static string ExtractSection(string text, string startMarker, string endMarker)
    {
        var start = text.IndexOf(startMarker, StringComparison.Ordinal);
        var end = text.IndexOf(endMarker, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"markers {startMarker} / {endMarker}");
        return text.Substring(start, end - start);
    }

    private static string ReadSettingsPage() =>
        File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "MusicVault.App", "Views", "Pages", "SettingsPageView.axaml"));

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MusicVault.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("MusicVault.sln not found from test output.");
    }
}
