using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace MusicVault.App.Services;

/// <summary>
/// 应用日志初始化器。配置 Serilog → 文件（滚动） + 调试输出。
/// </summary>
public static class AppLog
{
    private static ILoggerFactory? _factory;
    private static ILogger? _defaultLogger;

    /// <summary>全局默认 Logger（无泛型类别）。用于启动早期和静态上下文。</summary>
    public static ILogger Default => _defaultLogger ??= CreateLogger("MusicVault");

    /// <summary>为指定类别创建 Logger。</summary>
    public static ILogger CreateLogger(string category)
    {
        _factory ??= BuildFactory();
        return _factory.CreateLogger(category);
    }

    /// <summary>为类型 T 创建 Logger。</summary>
    public static ILogger<T> CreateLogger<T>() where T : class
    {
        _factory ??= BuildFactory();
        return _factory.CreateLogger<T>();
    }

    /// <summary>获取 DI 注入用的 ILoggerFactory。</summary>
    public static ILoggerFactory Factory
    {
        get
        {
            _factory ??= BuildFactory();
            return _factory;
        }
    }

    /// <summary>日志文件路径（供诊断导出）</summary>
    public static string LogFilePath { get; private set; } = string.Empty;

    private static ILoggerFactory BuildFactory()
    {
        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MusicVault");
        Directory.CreateDirectory(dataDir);
        LogFilePath = Path.Combine(dataDir, "logs", "musicvault-.log");
        Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath)!);

        var serilogLogger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http", LogEventLevel.Warning)
            .WriteTo.File(
                LogFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                encoding: System.Text.Encoding.UTF8)
            .CreateLogger();

        return new SerilogLoggerFactory(serilogLogger);
    }
}
