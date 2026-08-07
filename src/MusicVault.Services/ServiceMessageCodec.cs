namespace MusicVault.Services;

/// <summary>
/// 服务层语义化消息编码：持久化/队列中存 <c>svc:Key</c> 或 <c>svc:Key␟arg</c>，UI 按当前语言解析。
/// 非编码字符串（历史中文或第三方异常）原样展示。
/// </summary>
public static class ServiceMessageCodec
{
    public const string Prefix = "svc:";
    private const char ArgSeparator = '\u001f';
    private const char DimensionGroupSeparator = '|';

    public static string Key(string messageKey) => Prefix + messageKey;

    public static string Format(string messageKey, params object?[] args)
    {
        if (args is not { Length: > 0 })
            return Key(messageKey);

        return Prefix + messageKey + ArgSeparator + string.Join(ArgSeparator, args.Select(a => a?.ToString() ?? string.Empty));
    }

    public static bool IsEncoded(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.StartsWith(Prefix, StringComparison.Ordinal);

    public static string? TryGetKey(string? value)
    {
        if (!IsEncoded(value))
            return null;

        var body = value![Prefix.Length..];
        var sep = body.IndexOf(ArgSeparator);
        return sep >= 0 ? body[..sep] : body;
    }

    public static string Resolve(string? stored, string culture = "zh-CN")
    {
        if (string.IsNullOrWhiteSpace(stored))
            return string.Empty;

        if (!IsEncoded(stored))
            return stored;

        var body = stored[Prefix.Length..];
        var sep = body.IndexOf(ArgSeparator);
        var key = sep >= 0 ? body[..sep] : body;
        if (!ServiceMessages.All.TryGetValue(key, out var pair))
            return stored;

        var template = culture.StartsWith("en", StringComparison.OrdinalIgnoreCase)
            ? pair.EnUs
            : pair.ZhCn;

        if (sep < 0)
            return template;

        var args = body[(sep + 1)..].Split(ArgSeparator)
            .Select(a => ResolveArg(a, culture))
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

    public static string ResolveArg(string arg, string culture)
    {
        if (arg.Contains(DimensionGroupSeparator))
        {
            return string.Join(
                culture.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? ", " : "、",
                arg.Split(DimensionGroupSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(part => IsEncoded(part) ? Resolve(part, culture) : part));
        }

        return IsEncoded(arg) ? Resolve(arg, culture) : arg;
    }
}
