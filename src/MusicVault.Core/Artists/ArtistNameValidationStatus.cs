namespace MusicVault.Core.Artists;

/// <summary>艺术家名校验结果：仅 <see cref="NotRecognized"/> 应清理或拒绝入库。</summary>
public enum ArtistNameValidationStatus
{
    /// <summary>平台明确匹配到该艺人。</summary>
    Recognized,

    /// <summary>平台检索成功但未找到匹配（名称异常/厂牌等）。</summary>
    NotRecognized,

    /// <summary>平台未配置、网络错误或检索失败——保留现有数据，不做清理。</summary>
    Inconclusive
}
