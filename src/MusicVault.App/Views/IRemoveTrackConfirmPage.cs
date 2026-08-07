using MusicVault.App.ViewModels;

namespace MusicVault.App.Views;

/// <summary>曲目上下文菜单删除时弹出 Popconfirm 的页面。</summary>
public interface IRemoveTrackConfirmPage
{
    void RequestRemoveTrackConfirm(TrackItemViewModel item);
}
