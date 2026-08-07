using MusicVault.Services;

namespace MusicVault.App.Localization;

/// <summary>将服务层编码消息解析为当前 UI 语言。</summary>
public static class ServiceMessageLocalizer
{
    public static string Resolve(ILocalizationService l10n, string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return string.Empty;

        if (!ServiceMessageCodec.IsEncoded(stored))
            return stored;

        var key = ServiceMessageCodec.TryGetKey(stored);
        if (key is null)
            return stored;

        var localized = l10n.Get(key);
        if (!string.Equals(localized, key, StringComparison.Ordinal))
            return FormatIfNeeded(l10n, localized, stored);

        return ServiceMessageCodec.Resolve(stored, l10n.Culture);
    }

    public static string ResolveMany(ILocalizationService l10n, IEnumerable<string> messages, string separator = "；")
    {
        var parts = messages.Select(m => Resolve(l10n, m)).Where(s => s.Length > 0).ToArray();
        return parts.Length == 0 ? string.Empty : string.Join(separator, parts);
    }

    public static string Format(ILocalizationService l10n, string templateKey, string? detail) =>
        string.Format(l10n.Get(templateKey), Resolve(l10n, detail));

    /// <summary>解析单条或「 · 」拼接的多条服务层详情。</summary>
    public static string ResolveDetailAggregate(ILocalizationService l10n, string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return string.Empty;

        if (!stored.Contains(" · ", StringComparison.Ordinal))
            return Resolve(l10n, stored);

        return string.Join(
            " · ",
            stored.Split(" · ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => Resolve(l10n, part)));
    }

    private static string FormatIfNeeded(ILocalizationService l10n, string template, string stored)
    {
        var body = stored[ServiceMessageCodec.Prefix.Length..];
        var sep = body.IndexOf('\u001f');
        if (sep < 0)
            return template;

        var args = body[(sep + 1)..]
            .Split('\u001f')
            .Select(a => ServiceMessageCodec.ResolveArg(a, l10n.Culture))
            .Cast<object>()
            .ToArray();
        try
        {
            return string.Format(template, args);
        }
        catch
        {
            return template;
        }
    }
}
