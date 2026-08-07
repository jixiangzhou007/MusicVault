using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace MusicVault.Core.Diagnostics;

/// <summary>
/// 应用级性能指标注册表（System.Diagnostics.Metrics）。
/// 所有指标通过 <see cref="Meter"/> 暴露，可由 dotnet-counters / Prometheus 等工具采集。
/// 同时记录最近 N 条快照供应用内诊断页展示。
/// </summary>
public static class AppMetrics
{
    public const string MeterName = "MusicVault";

    private static readonly Meter _meter = new(MeterName, "1.0.0");

    // ── 计数器 ──
    public static readonly Counter<long> TracksScanned = _meter.CreateCounter<long>(
        "musicvault.tracks.scanned", "tracks", "已扫描曲目总数");

    public static readonly Counter<long> TracksImported = _meter.CreateCounter<long>(
        "musicvault.tracks.imported", "tracks", "成功入库曲目数");

    public static readonly Counter<long> DownloadsCompleted = _meter.CreateCounter<long>(
        "musicvault.downloads.completed", "downloads", "下载完成数");

    public static readonly Counter<long> DownloadsFailed = _meter.CreateCounter<long>(
        "musicvault.downloads.failed", "downloads", "下载失败数");

    public static readonly Counter<long> SearchesPerformed = _meter.CreateCounter<long>(
        "musicvault.searches.count", "searches", "搜索执行次数");

    // ── 直方图 ──
    public static readonly Histogram<double> ScanDuration = _meter.CreateHistogram<double>(
        "musicvault.scan.duration_ms", "ms", "扫描耗时分布");

    public static readonly Histogram<double> SearchDuration = _meter.CreateHistogram<double>(
        "musicvault.search.duration_ms", "ms", "搜索耗时分布");

    public static readonly Histogram<double> DownloadDuration = _meter.CreateHistogram<double>(
        "musicvault.download.duration_ms", "ms", "下载耗时分布");

    public static readonly Histogram<double> PlaybackStartupDuration = _meter.CreateHistogram<double>(
        "musicvault.playback.startup_ms", "ms", "播放启动耗时分布");

    // ── 可观测仪表 ──
    public static readonly ObservableGauge<int> LibraryTrackCount = _meter.CreateObservableGauge(
        "musicvault.library.track_count", () => _trackCount, "tracks", "库内曲目总数");

    private static int _trackCount;

    public static void SetLibraryTrackCount(int count) => _trackCount = count;

    // ── 诊断快照（应用内展示） ──
    private static readonly List<MetricSnapshot> _recentSnapshots = new(capacity: 64);
    private static readonly object _snapshotLock = new();

    public static IReadOnlyList<MetricSnapshot> RecentSnapshots
    {
        get { lock (_snapshotLock) return _recentSnapshots.ToArray(); }
    }

    /// <summary>记录一次带耗时的操作。</summary>
    public static void RecordOperation(string name, TimeSpan elapsed, bool success = true)
    {
        lock (_snapshotLock)
        {
            _recentSnapshots.Add(new MetricSnapshot(name, elapsed, success, DateTime.UtcNow));
            if (_recentSnapshots.Count > 64)
                _recentSnapshots.RemoveRange(0, _recentSnapshots.Count - 64);
        }
    }

    /// <summary>计时辅助：测量 Action 耗时并记录。</summary>
    public static T Measure<T>(string operationName, Func<T> action)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var result = action();
            RecordOperation(operationName, sw.Elapsed, success: true);
            return result;
        }
        catch
        {
            RecordOperation(operationName, sw.Elapsed, success: false);
            throw;
        }
    }

    /// <summary>计时辅助：测量异步 Action 耗时并记录。</summary>
    public static async Task<T> MeasureAsync<T>(string operationName, Func<Task<T>> action)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await action().ConfigureAwait(false);
            RecordOperation(operationName, sw.Elapsed, success: true);
            return result;
        }
        catch
        {
            RecordOperation(operationName, sw.Elapsed, success: false);
            throw;
        }
    }
}

/// <summary>诊断快照：单次操作耗时记录。</summary>
public sealed record MetricSnapshot(
    string Operation,
    TimeSpan Elapsed,
    bool Success,
    DateTime Timestamp)
{
    public string ElapsedDisplay => Elapsed.TotalMilliseconds < 1000
        ? $"{Elapsed.TotalMilliseconds:F0}ms"
        : $"{Elapsed.TotalSeconds:F2}s";

    public string TimestampDisplay => Timestamp.ToLocalTime().ToString("HH:mm:ss");
}
