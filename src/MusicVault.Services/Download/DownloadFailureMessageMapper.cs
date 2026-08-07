using System.Net.Http;
using MusicVault.Services;

namespace MusicVault.Services.Download;

/// <summary>下载失败 → 语义化消息（少把异常原文甩到 UI）。</summary>
public static class DownloadFailureMessageMapper
{
    public static string MapException(Exception ex)
    {
        if (ex is OperationCanceledException or TaskCanceledException or TimeoutException)
            return ServiceMessageCodec.Key("Download.Timeout");

        if (ex is HttpRequestException http)
            return MapHttp(http);

        if (ex is FileNotFoundException fnf)
        {
            if (ServiceMessageCodec.IsEncoded(fnf.Message))
                return fnf.Message;
            return ServiceMessageCodec.Key("Download.FileNotFound");
        }

        if (ex is IOException)
            return ServiceMessageCodec.Key("Download.IoError");

        if (!string.IsNullOrWhiteSpace(ex.Message) && ServiceMessageCodec.IsEncoded(ex.Message))
            return ex.Message;

        return MapRaw(ex.Message);
    }

    public static string MapStored(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return ServiceMessageCodec.Key("Download.FailedRetry");
        if (ServiceMessageCodec.IsEncoded(message))
            return message;
        return MapRaw(message);
    }

    private static string MapHttp(HttpRequestException ex)
    {
        var text = ex.Message ?? string.Empty;
        if (ContainsAny(text, "401", "403", "unauthorized", "forbidden", "鉴权", "cookie", "login"))
            return ServiceMessageCodec.Key("Download.AuthFailed");
        if (ContainsAny(text, "404", "not found"))
            return ServiceMessageCodec.Key("Download.NotFound");
        if (ContainsAny(text, "timeout", "timed out", "超时"))
            return ServiceMessageCodec.Key("Download.Timeout");
        return ServiceMessageCodec.Key("Download.NetworkError");
    }

    private static string MapRaw(string? msg)
    {
        if (string.IsNullOrWhiteSpace(msg))
            return ServiceMessageCodec.Key("Download.FailedRetry");

        if (ContainsAny(msg, "timeout", "timed out", "超时"))
            return ServiceMessageCodec.Key("Download.Timeout");
        if (ContainsAny(msg, "401", "403", "unauthorized", "forbidden", "鉴权", "cookie", "login", "登录"))
            return ServiceMessageCodec.Key("Download.AuthFailed");
        if (ContainsAny(msg, "404", "not found", "未找到", "找不到"))
            return ServiceMessageCodec.Key("Download.NotFound");
        if (ContainsAny(msg, "network", "connection", "connect", "无法连接", "连接失败", "socket", "ssl", "tls", "断网"))
            return ServiceMessageCodec.Key("Download.NetworkError");
        if (ContainsAny(msg, "disk", "space", "空间不足", "磁盘满", "ioexception"))
            return ServiceMessageCodec.Key("Download.IoError");

        // 过长或像堆栈的原文：收成通用失败
        if (msg.Length > 100
            || msg.Contains(" at ", StringComparison.Ordinal)
            || msg.Contains("Exception", StringComparison.Ordinal))
            return ServiceMessageCodec.Key("Download.FailedRetry");

        return ServiceMessageCodec.Format("Download.FailedWithDetail", Truncate(msg, 80));
    }

    private static string Truncate(string value, int max)
    {
        var t = value.Trim();
        return t.Length <= max ? t : t[..(max - 1)] + "…";
    }

    private static bool ContainsAny(string text, params string[] needles) =>
        needles.Any(n => text.Contains(n, StringComparison.OrdinalIgnoreCase));
}
