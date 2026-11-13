namespace MusicVault.App.ViewModels.Pages;

/// <summary>文件夹浏览中的一层子目录。</summary>
public sealed class FolderBrowseItemViewModel
{
    public FolderBrowseItemViewModel(string name, string absolutePath)
    {
        Name = name;
        AbsolutePath = absolutePath;
    }

    public string Name { get; }
    public string AbsolutePath { get; }
}
