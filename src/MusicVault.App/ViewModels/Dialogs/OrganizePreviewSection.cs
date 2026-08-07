namespace MusicVault.App.ViewModels.Dialogs;

/// <summary>整理预览中的一个维度（文件夹或文件名），各含一条示例。</summary>
public sealed record OrganizePreviewSection(
    string DimensionTitle,
    string BeforeLabel,
    string AfterLabel,
    string Before,
    string After);
