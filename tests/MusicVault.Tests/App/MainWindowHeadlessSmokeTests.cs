using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MusicVault.App.Services;
using MusicVault.App.ViewModels.Pages;
using MusicVault.Core.Enums;

namespace MusicVault.Tests.App;

/// <summary>主窗口 Headless 冒烟：单次会话覆盖顶栏、分区导航、发现/传输与主题。</summary>
[Collection("HeadlessUiSerial")]
public sealed class MainWindowHeadlessSmokeTests
{
    [AvaloniaFact]
    public async Task MainWindow_ShellSmoke_CoversNavigationAndSettings()
    {
        await using var host = await HeadlessUiTestHost.CreateAsync();
        await AssertShellSmokeAsync(host);
    }

    private static async Task AssertShellSmokeAsync(HeadlessUiTestHost host)
    {
        var shell = host.Shell;
        var window = host.Window;
        var l10n = host.Localization;

        Assert.True(window.IsVisible);
        Assert.Equal(l10n.Get("Nav.Library"), shell.NavLibrary);
        Assert.Equal(l10n.Get("Nav.Discover"), shell.NavDiscover);
        Assert.Equal(l10n.Get("Nav.Transfers"), shell.NavTransfers);
        Assert.Equal(l10n.Get("Nav.Tools"), shell.NavTools);
        Assert.Equal(l10n.Get("Nav.Settings"), shell.NavSettings);

        var labels = window.GetVisualDescendants()
            .OfType<Button>()
            .Select(b => b.Content?.ToString())
            .Where(static s => !string.IsNullOrWhiteSpace(s))
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains(shell.NavLibrary, labels);
        Assert.Contains(shell.NavHome, labels);
        Assert.Contains(shell.NavSettings, labels);

        var navTabLabels = window.GetVisualDescendants()
            .OfType<Button>()
            .Where(b => b.Classes.Contains("nav-tab"))
            .Select(b => b.Content?.ToString())
            .Where(static s => !string.IsNullOrWhiteSpace(s))
            .ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain(shell.NavTransfers, navTabLabels);

        var transfersButton = window.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(b => AutomationProperties.GetName(b) == shell.NavTransfers);
        Assert.NotNull(transfersButton);
        Assert.Contains("top-icon-btn", transfersButton.Classes);

        var chromePanel = window.GetVisualDescendants()
            .OfType<StackPanel>()
            .FirstOrDefault(p => p.Name == "WindowChromePanel");
        Assert.NotNull(chromePanel);
        Assert.True(chromePanel.IsVisible);

        Assert.True(shell.ShowFullShell);
        Assert.False(shell.IsCompactPlayerMode);
        Assert.NotNull(shell.Player);

        AssertSection<SearchPageViewModel>(host, NavigationSection.Search);
        AssertDiscoverBrowseHubSessionToggles(shell.Search);
        AssertSection<DownloadsPageViewModel>(host, NavigationSection.Downloads);
        AssertSection<AudioToolsPageViewModel>(host, NavigationSection.AudioTools);
        AssertSection<SettingsPageViewModel>(host, NavigationSection.Settings);

        var settings = shell.Settings;
        var titles = settings.NavItems.Select(n => n.Title).ToList();
        Assert.Contains(l10n.Get("Settings.Section.Overview"), titles);
        Assert.Contains(l10n.Get("Settings.Section.Playback"), titles);
        Assert.Contains(l10n.Get("Settings.Section.DownloadCache"), titles);
        Assert.Contains(l10n.Get("Settings.Section.Advanced"), titles);
        Assert.Contains(l10n.Get("Settings.Section.UpdateDiagnostics"), titles);

        var search = shell.Search;
        search.ApplyExternalSearch("晴天", "周杰伦");
        Assert.Equal("晴天", search.SongNameQuery);
        Assert.Equal("周杰伦", search.ArtistQuery);
        Assert.True(search.IsSearchMode);

        var downloads = shell.Downloads;
        downloads.SelectFailedPresetCommand.Execute(null);
        Assert.Equal(2, downloads.ViewPresetIndex);
        downloads.SelectQueuePresetCommand.Execute(null);
        Assert.Equal(0, downloads.ViewPresetIndex);

        var themeBefore = shell.Theme.CurrentMode;
        var themeNext = themeBefore == AppThemeMode.Light ? AppThemeMode.Dark : AppThemeMode.Light;
        shell.Theme.Apply(themeNext);
        var expectedVariant = themeNext == AppThemeMode.Dark ? ThemeVariant.Dark : ThemeVariant.Light;
        Assert.Equal(expectedVariant, Application.Current?.RequestedThemeVariant);

        var experimentalNote = l10n.Get("Discover.Platform.ExperimentalToolTipNote");
        var qqCapsule = search.SearchModeLoginCapsules.First(c => c.Platform == MusicPlatform.QQMusic);
        Assert.DoesNotContain(experimentalNote, qqCapsule.DisplayText, StringComparison.Ordinal);
        Assert.Contains(experimentalNote, qqCapsule.ToolTip, StringComparison.Ordinal);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Discover shell：细搜条常驻；无会话时 feed/hub；有会话时细条下出结果；清搜索回到 feed。
    /// 须在同一 Headless 会话内断言（勿再开第二个 MainWindow AvaloniaFact）。
    /// </summary>
    private static void AssertDiscoverBrowseHubSessionToggles(SearchPageViewModel search)
    {
        Assert.False(search.HasActiveSearchSession);
        Assert.True(search.ShowBrowseHub);
        Assert.True(search.UseCompactSearch);
        Assert.False(search.UseHeroSearch);
        Assert.False(search.ShowSearchResultsPane);

        search.HasActiveSearchSession = true;
        Assert.False(search.ShowBrowseHub);
        Assert.True(search.UseCompactSearch);
        Assert.True(search.ShowSearchResultsPane);

        search.ClearSearchCriteriaCommand.Execute(null);
        Assert.False(search.HasActiveSearchSession);
        Assert.True(search.ShowBrowseHub);
        Assert.False(search.ShowSearchResultsPane);

        // 有会话但条件已空：点发现 / 恢复 idle 应回到 feed
        search.HasActiveSearchSession = true;
        search.SongNameQuery = string.Empty;
        search.ArtistQuery = string.Empty;
        search.RestoreBrowseHubIfIdle();
        Assert.False(search.HasActiveSearchSession);
        Assert.True(search.ShowBrowseHub);
    }

    private static void AssertSection<TPage>(HeadlessUiTestHost host, NavigationSection section)
        where TPage : PageViewModelBase
    {
        host.Shell.NavigateCommand.Execute(section);
        Assert.Equal(section, host.Shell.CurrentSection);
        Assert.IsType<TPage>(host.Shell.CurrentPage);
    }
}
