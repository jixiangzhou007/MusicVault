namespace MusicVault.Core.Gatherers;

/// <summary>采集源类型（供设置页与未来管理扩展）</summary>
public enum MusicGathererKind
{
    /// <summary>统一多平台 API（go-music-api）</summary>
    UnifiedApi,
    /// <summary>单平台独立 HTTP 服务（网易云 API、QQMusicApi 等）</summary>
    ServiceApi,
    /// <summary>官方平台直连搜索</summary>
    PlatformDirect,
    /// <summary>第三方聚合站（MyFreeMP3、JBSou 等）</summary>
    Aggregator
}
