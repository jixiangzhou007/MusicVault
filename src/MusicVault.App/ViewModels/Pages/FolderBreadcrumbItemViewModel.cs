using CommunityToolkit.Mvvm.Input;

namespace MusicVault.App.ViewModels.Pages;

/// <summary>文件夹浏览路径上的一节，点击回到该层。</summary>
public sealed class FolderBreadcrumbItemViewModel
{
    public FolderBreadcrumbItemViewModel(
        string label,
        string folderAbsolutePath,
        bool isCurrent,
        bool showSeparator,
        Action<string> navigate)
    {
        Label = label;
        FolderAbsolutePath = folderAbsolutePath;
        IsCurrent = isCurrent;
        ShowSeparator = showSeparator;
        NavigateCommand = new RelayCommand(() => navigate(folderAbsolutePath), () => !isCurrent);
    }

    public string Label { get; }
    public string FolderAbsolutePath { get; }
    public bool IsCurrent { get; }
    public bool ShowSeparator { get; }
    public IRelayCommand NavigateCommand { get; }
}
