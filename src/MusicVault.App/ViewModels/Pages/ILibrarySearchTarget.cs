namespace MusicVault.App.ViewModels.Pages;

/// <summary>可由主窗口浏览条统一触发本地搜索的页面。</summary>
public interface ILibrarySearchTarget
{
    void ApplyLibrarySearch(string query);
}
