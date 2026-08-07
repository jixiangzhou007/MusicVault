using MusicVault.App.ViewModels;

namespace MusicVault.App.Views;

/// <summary>曲目列表页接入 <see cref="TrackListInteraction"/> 时的可选行为。</summary>
public sealed class TrackListPageOptions
{
    /// <summary>滚动接近底部时加载更多。</summary>
    public Func<Task>? LoadMoreAsync { get; init; }

    /// <summary>虚拟列表集合变化时刷新 ItemsSource（仅首页需要）。</summary>
    public VirtualTrackCollection? Tracks { get; init; }

    /// <summary>是否订阅 Shell 的「正在播放」选中同步。</summary>
    public bool SyncNowPlaying { get; init; } = true;

    /// <summary>右键菜单提供「加入整理队列」（仅全部歌曲页启用）。</summary>
    public bool ShowOrganizeContextMenu { get; init; }
}
