using System.Net;
using System.Net.Http;
using MusicVault.App.Localization;
using MusicVault.Services;
using MusicVault.Services.Api;

namespace MusicVault.App.Services;

/// <summary>在线播放失败时的用户友好提示文案。</summary>
public static class OnlinePlaybackStatusText
{
    public const string NoLinkHintKey = "Discover.Playback.NoLinkHint";
    public const string ApiNotConfiguredHintKey = "Discover.Playback.ApiNotConfiguredHint";

    /// <summary>编码后的无链接提示（供服务层抛出）。</summary>
    public static string NoLinkHintStored => ServiceMessageCodec.Key(NoLinkHintKey);

    /// <summary>编码后的 API 未配置提示。</summary>
    public static string ApiNotConfiguredHintStored => ServiceMessageCodec.Key(ApiNotConfiguredHintKey);

    public static string FormatUnavailable(ILocalizationService l10n, string songTitle, Exception? ex = null)
    {
        var reason = ResolveReason(l10n, ex);
        return string.IsNullOrWhiteSpace(songTitle)
            ? reason
            : $"{songTitle} · {reason}";
    }

    public static string ResolveReason(ILocalizationService l10n, Exception? ex)
    {
        if (ex == null)
            return l10n.Get(NoLinkHintKey);

        if (ex is InvalidOperationException invalidOp)
        {
            if (IsNoPlayLinkMessage(invalidOp.Message))
                return l10n.Get(NoLinkHintKey);

            if (ServiceMessageCodec.TryGetKey(invalidOp.Message) is NoLinkHintKey or ApiNotConfiguredHintKey)
                return ServiceMessageLocalizer.Resolve(l10n, invalidOp.Message);
        }

        if (!GoMusicApiClient.IsConfigured)
            return l10n.Get(ApiNotConfiguredHintKey);

        if (IsCommonPlaybackFailure(ex))
            return l10n.Get(NoLinkHintKey);

        return l10n.Get(NoLinkHintKey);
    }

    public static bool IsNoPlayLinkMessage(string? message) =>
        ServiceMessageCodec.TryGetKey(message) is "Preview.NoPlayLink" or NoLinkHintKey
        || message is "无法获取在线播放链接" or "无法获取试听链接"
        || string.Equals(message, "Cannot get online playback link", StringComparison.OrdinalIgnoreCase);

    public static bool IsAuthRelatedPlaybackFailure(string? statusMessage, ILocalizationService l10n)
    {
        if (string.IsNullOrWhiteSpace(statusMessage))
            return false;

        if (DiscoverPlatformAuthSignals.ShouldRefreshLoginState([statusMessage]))
            return true;

        var noLink = l10n.Get(NoLinkHintKey);
        var apiHint = l10n.Get(ApiNotConfiguredHintKey);
        return statusMessage.Contains(noLink, StringComparison.Ordinal)
               || statusMessage.Contains(apiHint, StringComparison.Ordinal)
               || statusMessage.Contains(ServiceMessageCodec.Key(NoLinkHintKey), StringComparison.Ordinal)
               || statusMessage.Contains(ServiceMessageCodec.Key(ApiNotConfiguredHintKey), StringComparison.Ordinal);
    }

    public static bool IsCommonPlaybackFailure(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            if (current is InvalidOperationException invalidOp && IsNoPlayLinkMessage(invalidOp.Message))
                return true;

            if (current is HttpRequestException)
                return true;

            if (current is WebException)
                return true;

            var message = current.Message;
            if (string.IsNullOrWhiteSpace(message))
                continue;

            if (message.Contains("404", StringComparison.Ordinal)
                || message.Contains("401", StringComparison.Ordinal)
                || message.Contains("403", StringComparison.Ordinal)
                || message.Contains("Not Found", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Response status code does not indicate success", StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
