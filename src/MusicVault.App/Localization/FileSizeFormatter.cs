namespace MusicVault.App.Localization;

/// <summary>文件大小展示（B/KB/MB/GB）。</summary>
public static class FileSizeFormatter
{
    public static string Format(long bytes, ILocalizationService l10n)
    {
        if (bytes < 1024)
            return string.Format(l10n.Get("Common.FileSize.Bytes"), bytes);

        var kb = bytes / 1024.0;
        if (kb < 1024)
            return string.Format(l10n.Get("Common.FileSize.Kilobytes"), kb.ToString("F1"));

        var mb = kb / 1024.0;
        if (mb < 1024)
            return string.Format(l10n.Get("Common.FileSize.Megabytes"), mb.ToString("F1"));

        return string.Format(l10n.Get("Common.FileSize.Gigabytes"), (mb / 1024.0).ToString("F2"));
    }
}
