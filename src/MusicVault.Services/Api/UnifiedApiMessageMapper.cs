using MusicVault.Services;

namespace MusicVault.Services.Api;

/// <summary>将 go-music-api / 网络异常映射为可本地化的语义消息（禁止把后端原文塞进 UI）。</summary>
public static class UnifiedApiMessageMapper
{
    public static string MapHttpFailure(string? apiMsg)
    {
        if (string.IsNullOrWhiteSpace(apiMsg))
            return ServiceMessageCodec.Key("Api.Unified.RequestFailedDefault");

        if (ServiceMessageCodec.IsEncoded(apiMsg))
            return apiMsg;

        if (LooksLikeAuth(apiMsg))
            return ServiceMessageCodec.Key("Api.Unified.NeedLogin");

        if (ContainsAny(apiMsg, "cookie", "Cookie", "未授权", "无权限", "forbidden", "Forbidden"))
            return ServiceMessageCodec.Key("Api.Unified.AuthOrCookie");

        if (ContainsAny(apiMsg, "timeout", "超时", "timed out"))
            return ServiceMessageCodec.Key("Api.Unified.Timeout");

        return ServiceMessageCodec.Key("Api.Unified.RequestFailedDefault");
    }

    public static string MapException(Exception ex)
    {
        if (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            if (ex is TaskCanceledException or OperationCanceledException)
                return ServiceMessageCodec.Key("Api.Unified.Timeout");
            return ServiceMessageCodec.Key("Api.Unified.NetworkError");
        }

        var msg = ex.Message ?? string.Empty;
        if (LooksLikeAuth(msg))
            return ServiceMessageCodec.Key("Api.Unified.NeedLogin");

        return ServiceMessageCodec.Key("Api.Unified.SearchFailedGeneric");
    }

    public static bool IsAuthRelatedKey(string? messageKey) =>
        messageKey is "Api.Unified.NeedLogin"
            or "Api.Unified.AuthOrCookie"
            or "Api.Unified.QqSearchNoResults"
            or "Api.Unified.QqLoggedInNoResults";

    private static bool LooksLikeAuth(string text) =>
        ContainsAny(
            text,
            "登录", "未登录", "login", "Login", "sign in", "Sign in",
            "unauthorized", "Unauthorized", "401", "403", "authentication");

    private static bool ContainsAny(string text, params string[] needles)
    {
        foreach (var n in needles)
        {
            if (text.Contains(n, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
