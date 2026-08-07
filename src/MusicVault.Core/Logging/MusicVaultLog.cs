using Microsoft.Extensions.Logging;

namespace MusicVault.Core.Logging;

/// <summary>
/// 应用级日志定义（Source Generator 模式，高性能结构化日志）。
/// </summary>
public static partial class MusicVaultLog
{
    // ── 启动与生命周期 ──

    [LoggerMessage(Level = LogLevel.Information, Message = "MusicVault 启动 v{Version} ({Platform})")]
    public static partial void AppStarted(this ILogger logger, string version, string platform);

    [LoggerMessage(Level = LogLevel.Information, Message = "MusicVault 正在关闭")]
    public static partial void AppShuttingDown(this ILogger logger);

    // ── 播放器 ──

    [LoggerMessage(Level = LogLevel.Error, Message = "播放失败: {FilePath} — {ErrorMessage}")]
    public static partial void PlaybackFailed(this ILogger logger, string filePath, string errorMessage);

    [LoggerMessage(Level = LogLevel.Warning, Message = "macOS 原生播放器 dylib 不可用，播放功能受限")]
    public static partial void MacOsDylibUnavailable(this ILogger logger);

    // ── 扫描 ──

    [LoggerMessage(Level = LogLevel.Information, Message = "曲库扫描完成: {LibraryName}, 导入 {Imported} 首, 失败 {Failed} 首")]
    public static partial void LibraryScanCompleted(this ILogger logger, string libraryName, int imported, int failed);

    [LoggerMessage(Level = LogLevel.Warning, Message = "扫描曲目失败: {FilePath} — {ErrorMessage}")]
    public static partial void LibraryScanTrackFailed(this ILogger logger, string filePath, string errorMessage);

    // ── 下载 ──

    [LoggerMessage(Level = LogLevel.Error, Message = "下载失败 [{Platform}] {Title}: {ErrorMessage}")]
    public static partial void DownloadFailed(this ILogger logger, string platform, string title, string errorMessage);

    [LoggerMessage(Level = LogLevel.Information, Message = "下载完成 [{Platform}] {Title} → {Path}")]
    public static partial void DownloadCompleted(this ILogger logger, string platform, string title, string path);

    // ── 在线 API ──

    [LoggerMessage(Level = LogLevel.Warning, Message = "API 不可用 [{Platform}]: {Endpoint} — {Reason}")]
    public static partial void ApiUnavailable(this ILogger logger, string platform, string endpoint, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "API 调用异常 [{Platform}] {Endpoint}: {ErrorMessage}")]
    public static partial void ApiCallFailed(this ILogger logger, string platform, string endpoint, string errorMessage);

    // ── 数据库 ──

    [LoggerMessage(Level = LogLevel.Information, Message = "数据库迁移完成: v{FromVersion} → v{ToVersion}")]
    public static partial void DatabaseMigrated(this ILogger logger, int fromVersion, int toVersion);

    [LoggerMessage(Level = LogLevel.Error, Message = "数据库操作失败: {Operation} — {ErrorMessage}")]
    public static partial void DatabaseOperationFailed(this ILogger logger, string operation, string errorMessage);

    // ── NAS / 网络 ──

    [LoggerMessage(Level = LogLevel.Warning, Message = "NAS 挂载失败 [{LibraryName}]: {ErrorMessage}")]
    public static partial void NasMountFailed(this ILogger logger, string libraryName, string errorMessage);

    [LoggerMessage(Level = LogLevel.Warning, Message = "WebDAV 连接失败 [{LibraryName}]: {ErrorMessage}")]
    public static partial void WebDavConnectFailed(this ILogger logger, string libraryName, string errorMessage);

    // ── 通用 ──

    [LoggerMessage(Level = LogLevel.Error, Message = "未处理异常: {ErrorMessage}")]
    public static partial void UnhandledError(this ILogger logger, string errorMessage);

    [LoggerMessage(Level = LogLevel.Warning, Message = "非关键操作失败（已降级）: {Context} — {ErrorMessage}")]
    public static partial void NonCriticalFailure(this ILogger logger, string context, string errorMessage);
}
