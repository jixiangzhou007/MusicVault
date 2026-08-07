using System.Text.Json;

namespace MusicVault.App.Services;

public static class UsageInsightKeys
{
    public const string SearchAttempt = "search.attempt";
    public const string SearchSuccess = "search.success";
    public const string ParseAttempt = "parse.attempt";
    public const string ParseSuccess = "parse.success";
    public const string RecognizeAttempt = "recognize.attempt";
    public const string RecognizeSuccess = "recognize.success";
    public const string SearchToDownloads = "nav.search_to_downloads";
    public const string DownloadsToSearch = "nav.downloads_to_search";
    public const string OpenOnlineSettings = "nav.open_online_settings";
    public const string SettingsSave = "settings.save";
    public const string SettingsApplyRecommended = "settings.apply_recommended";
    public const string PlayFromDownloads = "play.downloads";
}

public sealed class UsageInsightsService
{
    private readonly AppSettingsService _settings;

    public UsageInsightsService(AppSettingsService settings) => _settings = settings;

    public event EventHandler? CountersChanged;

    public int GetCount(string key)
    {
        if (_settings.Current.UsageCounters.TryGetValue(key, out var value))
            return Math.Max(0, value);
        return 0;
    }

    public void Track(string key, int delta = 1)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        var safeDelta = Math.Max(1, delta);
        var counters = _settings.Current.UsageCounters;
        counters.TryGetValue(key, out var current);
        counters[key] = Math.Max(0, current + safeDelta);

        var dayKey = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var daily = _settings.Current.UsageDailyCounters;
        if (!daily.TryGetValue(dayKey, out var dayCounters))
        {
            dayCounters = new Dictionary<string, int>(StringComparer.Ordinal);
            daily[dayKey] = dayCounters;
        }

        dayCounters.TryGetValue(key, out var dayCurrent);
        dayCounters[key] = Math.Max(0, dayCurrent + safeDelta);

        _settings.Current.UsageCountersUpdatedAt = DateTime.UtcNow;
        CountersChanged?.Invoke(this, EventArgs.Empty);
        _ = _settings.SaveAsync();
    }

    public void ResetAll()
    {
        _settings.Current.UsageCounters.Clear();
        _settings.Current.UsageDailyCounters.Clear();
        _settings.Current.UsageCountersUpdatedAt = DateTime.UtcNow;
        CountersChanged?.Invoke(this, EventArgs.Empty);
        _ = _settings.SaveAsync();
    }

    public IReadOnlyList<int> GetLastNDaysSeries(string key, int days)
    {
        if (days <= 0)
            return [];

        var result = new int[days];
        var daily = _settings.Current.UsageDailyCounters;
        var today = DateTime.UtcNow.Date;
        for (var i = 0; i < days; i++)
        {
            var day = today.AddDays(i - (days - 1));
            var dayKey = day.ToString("yyyy-MM-dd");
            if (daily.TryGetValue(dayKey, out var counters) && counters.TryGetValue(key, out var value))
                result[i] = Math.Max(0, value);
        }

        return result;
    }

    public async Task<string> ExportSnapshotAsync(string outputDirectory, CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var filePath = Path.Combine(outputDirectory, $"usage-insights-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        var payload = new
        {
            generatedAtUtc = DateTime.UtcNow,
            updatedAtUtc = _settings.Current.UsageCountersUpdatedAt,
            totals = _settings.Current.UsageCounters,
            daily = _settings.Current.UsageDailyCounters
        };

        await using var fs = File.Create(filePath);
        await JsonSerializer.SerializeAsync(fs, payload, new JsonSerializerOptions { WriteIndented = true }, ct);
        return filePath;
    }
}
