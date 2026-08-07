namespace MusicVault.App.Layout;

/// <summary>曲目列表表格列宽与行高（全应用单一来源，见 docs/DESIGN_SYSTEM.md §14）。</summary>
public static class TrackTableLayout
{
    /// <summary>库内曲目：# · 歌曲 · 艺术家 · 专辑 · 年份 · 时长 · 收藏 · 歌单 · 操作</summary>
    public const string LibraryColumns = "40,3*,2*,2*,56,64,40,40,40";

    /// <summary>紧凑六列：序号/次数 · 歌曲 · 艺术家 · 专辑 · 时长 · 收藏（首页历史/常听/推荐）</summary>
    public const string CompactSixColumns = "56,3*,2*,2*,64,48";

    /// <summary>侧栏队列：# · 歌曲 · 艺术家 · 时长 · 收藏</summary>
    public const string SidebarQueueColumns = "40,5*,2.5*,0.9*,0.9*";

    /// <summary>发现/在线：# · 封面 · 歌曲(含歌手) · 来源 · 时长 · 缓存 · 收藏 · 歌单 · 操作</summary>
    public const string DiscoverColumns = "36,48,*,100,52,40,36,36,40";

    /// <summary>发现/在线（含批量勾选列）：选 · # · 封面 · 歌曲 · 来源 · 时长 · 缓存 · 收藏 · 歌单 · 操作</summary>
    public const string DiscoverBatchColumns = "32,36,48,*,90,48,36,36,36,40";

    public const string BatchScrapeColumns = "40,36,5*,2.5*,2.5*,56,44,44,44,48";

    /// <summary>传输队列：选 · # · 歌曲 · 歌手 · 状态 · 格式 · 大小 · 操作</summary>
    public const string TransferQueueColumns = "32,40,*,*,52,56,72,248";

    /// <summary>组合标签搜索：封面 · 平台 · 歌名 · 歌手 · 专辑 · 年份 · 时长 · 曲目 · 风格 · 作曲 · 音质 · 匹配</summary>
    public const string CombinedLabelsColumns = "52,88,2*,1.6*,1.8*,56,56,48,1.2*,1.2*,64,56";

    /// <summary>发现页传输速览侧栏：# · 点 · 歌曲 · 歌手 · 格式</summary>
    public const string TransferQuickViewColumns = "24,6,4*,2.5*,38";

    public const double HeaderHeight = 27;
    public const double RowHeight = 34;
    public const double DiscoverRowHeight = 56;
}
