namespace MusicVault.App.Controls;

internal static class ResultStatusGlyphs
{
    public static string GetGlyph(ResultStatus status) => status switch
    {
        ResultStatus.Success => "\uE73E",
        ResultStatus.Error => "\uE783",
        ResultStatus.Warning => "\uE7BA",
        _ => "\uE946",
    };

    public static string GetStatusClass(ResultStatus status) => status switch
    {
        ResultStatus.Success => "ant-result-icon-success",
        ResultStatus.Error => "ant-result-icon-error",
        ResultStatus.Warning => "ant-result-icon-warning",
        _ => "ant-result-icon-info",
    };
}
