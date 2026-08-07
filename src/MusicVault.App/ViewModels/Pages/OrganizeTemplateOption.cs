namespace MusicVault.App.ViewModels.Pages;

/// <summary>文件整理模板下拉项；<see cref="Template"/> 为 null 表示不整理该维度。</summary>
public sealed class OrganizeTemplateOption(string displayName, string? template)
{
    public string DisplayName { get; } = displayName;

    public string? Template { get; } = template;

    public override string ToString() => DisplayName;
}
