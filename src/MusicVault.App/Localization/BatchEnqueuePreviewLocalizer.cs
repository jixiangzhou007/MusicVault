using MusicVault.App.Localization;
using MusicVault.Services.Download;

namespace MusicVault.App.Localization;

/// <summary>批量入队预览弹窗文案解析。</summary>
public static class BatchEnqueuePreviewLocalizer
{
    public static string QualityLabel(ILocalizationService l10n, BatchEnqueuePreview preview) =>
        ServiceMessageLocalizer.Resolve(l10n, preview.QualityLabelKey);

    public static string EstimatedSizeLabel(ILocalizationService l10n, BatchEnqueuePreview preview)
    {
        var bytes = preview.EstimateTotalBytes();
        return bytes > 0 ? FileSizeFormatter.Format(bytes, l10n) : "—";
    }
}
