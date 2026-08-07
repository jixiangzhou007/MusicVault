using Avalonia.Headless.XUnit;
using MusicVault.App.Localization;
using MusicVault.App.Services;
using MusicVault.App.ViewModels.Pages;
using MusicVault.App.ViewModels.Player;
using MusicVault.Services;

namespace MusicVault.Tests.App;

/// <summary>传输页各视图预设的筛选/排序记忆（启动恢复与切换往返）。</summary>
[Collection("HeadlessUiSerial")]
public sealed class TransfersFilterPersistenceHeadlessTests
{
    [AvaloniaFact]
    public async Task DownloadsPage_RestoresPerPresetFiltersOnStartupAndPresetSwitch()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "MusicVault-transfers-filter-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        Environment.SetEnvironmentVariable(HeadlessUiTestHost.TestDataDirEnvVar, tempDir);

        try
        {
            var services = new AppServices();
            await services.InitializeCoreAsync();

            var settings = new AppSettingsService(services.DataDirectory);
            await settings.LoadAsync();
            settings.Current.CopyrightAccepted = true;
            settings.Current.FirstRunWizardCompleted = true;
            settings.Current.Culture = "zh-CN";
            settings.Current.TransfersViewPresetIndex = 0;
            settings.Current.TransfersQueueFilterText = "queue-keyword";
            settings.Current.TransfersHistoryFilterText = "history-keyword";
            settings.Current.TransfersFailedFilterText = "failed-keyword";
            settings.Current.TransfersQueueSortIndex = 1;
            settings.Current.TransfersHistorySortIndex = 2;
            settings.Current.TransfersFailedSortIndex = 0;
            await settings.SaveAsync();

            var localization = new LocalizationService(settings);
            var insights = new UsageInsightsService(settings);
            var player = new PlayerBarViewModel(services, settings, localization);
            var downloads = new DownloadsPageViewModel(services, settings, insights, player, localization);

            Assert.Equal("queue-keyword", downloads.FilterText);
            Assert.Equal(1, downloads.SortIndex);

            downloads.SelectHistoryPresetCommand.Execute(null);
            Assert.Equal("history-keyword", downloads.FilterText);
            Assert.Equal(2, downloads.SortIndex);

            downloads.SelectFailedPresetCommand.Execute(null);
            Assert.Equal("failed-keyword", downloads.FilterText);
            Assert.Equal(0, downloads.SortIndex);

            downloads.SelectQueuePresetCommand.Execute(null);
            Assert.Equal("queue-keyword", downloads.FilterText);
            Assert.Equal(1, downloads.SortIndex);

            downloads.FilterInputText = "queue-updated";
            downloads.ApplyFilterSearchCommand.Execute(null);
            Assert.Equal("queue-updated", settings.Current.TransfersQueueFilterText);

            var downloadsReloaded = new DownloadsPageViewModel(services, settings, insights, player, localization);
            Assert.Equal("queue-updated", downloadsReloaded.FilterText);
            Assert.Equal(1, downloadsReloaded.SortIndex);
        }
        finally
        {
            Environment.SetEnvironmentVariable(HeadlessUiTestHost.TestDataDirEnvVar, null);
            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // ignore cleanup failures
            }
        }
    }
}
