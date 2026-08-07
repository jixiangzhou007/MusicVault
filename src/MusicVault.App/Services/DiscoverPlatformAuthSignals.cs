using MusicVault.Services;
using MusicVault.Services.Api;

namespace MusicVault.App.Services;

/// <summary>发现页搜索/解析结果是否暗示 Cookie 失效，需刷新平台登录状态。</summary>
public static class DiscoverPlatformAuthSignals
{
    public static bool ShouldRefreshLoginState(Exception? ex) =>
        ex != null && OnlinePlaybackStatusText.IsCommonPlaybackFailure(ex);

    public static bool ShouldRefreshLoginState(IEnumerable<string>? diagnostics)
    {
        if (diagnostics == null)
            return false;

        foreach (var line in diagnostics)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var key = ServiceMessageCodec.TryGetKey(line);
            if (key != null && UnifiedApiMessageMapper.IsAuthRelatedKey(key))
                return true;

            if (line.Contains("登录", StringComparison.Ordinal)
                || line.Contains("未登录", StringComparison.Ordinal)
                || line.Contains("log in", StringComparison.OrdinalIgnoreCase)
                || line.Contains("logged in", StringComparison.OrdinalIgnoreCase)
                || line.Contains("not logged in", StringComparison.OrdinalIgnoreCase)
                || line.Contains("login", StringComparison.OrdinalIgnoreCase)
                || line.Contains("sign in", StringComparison.OrdinalIgnoreCase)
                || line.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)
                || line.Contains("authentication", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Cookie", StringComparison.OrdinalIgnoreCase)
                || line.Contains("401", StringComparison.Ordinal)
                || line.Contains("403", StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
