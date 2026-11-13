using MusicVault.App.Icons;

namespace MusicVault.App.Controls;

internal static class ResultStatusGlyphs
{
    public static string GetGlyph(ResultStatus status) => status switch
    {
        ResultStatus.Success => IconGlyphs.CheckMark,
        ResultStatus.Error => IconGlyphs.Error,
        ResultStatus.Warning => IconGlyphs.Warning,
        _ => IconGlyphs.Info,
    };

    public static string GetStatusClass(ResultStatus status) => status switch
    {
        ResultStatus.Success => "ant-result-icon-success",
        ResultStatus.Error => "ant-result-icon-error",
        ResultStatus.Warning => "ant-result-icon-warning",
        _ => "ant-result-icon-info",
    };
}
