using MusicVault.Core.Gatherers;

namespace MusicVault.Services.Gatherers;

/// <summary>采集源 DisplayName / Description 的 i18n key 映射。</summary>
public static class GathererMessageKeys
{
    public static string DisplayName(string gathererId) =>
        ServiceMessageCodec.Key(gathererId switch
        {
            MusicGathererIds.UnifiedGoMusicApi => "Gatherer.Name.UnifiedGoMusicApi",
            MusicGathererIds.NetEaseApi => "Gatherer.Name.NetEaseApi",
            MusicGathererIds.QqMusicApi => "Gatherer.Name.QqMusicApi",
            MusicGathererIds.PlatformKugou => "Gatherer.Name.PlatformKugou",
            MusicGathererIds.PlatformKuwo => "Gatherer.Name.PlatformKuwo",
            MusicGathererIds.PlatformQq => "Gatherer.Name.PlatformQq",
            MusicGathererIds.PlatformNetEase => "Gatherer.Name.PlatformNetEase",
            MusicGathererIds.PlatformMigu => "Gatherer.Name.PlatformMigu",
            MusicGathererIds.AggregatorMyFreeMp3 => "Gatherer.Name.AggregatorMyFreeMp3",
            MusicGathererIds.AggregatorJbsou => "Gatherer.Name.AggregatorJbsou",
            _ => "General.Unknown"
        });

    public static string Description(string gathererId) =>
        ServiceMessageCodec.Key(gathererId switch
        {
            MusicGathererIds.UnifiedGoMusicApi => "Gatherer.Desc.UnifiedGoMusicApi",
            MusicGathererIds.NetEaseApi => "Gatherer.Desc.NetEaseApi",
            MusicGathererIds.QqMusicApi => "Gatherer.Desc.QqMusicApi",
            MusicGathererIds.PlatformKugou => "Gatherer.Desc.PlatformKugou",
            MusicGathererIds.PlatformKuwo => "Gatherer.Desc.PlatformKuwo",
            MusicGathererIds.PlatformQq => "Gatherer.Desc.PlatformQq",
            MusicGathererIds.PlatformNetEase => "Gatherer.Desc.PlatformNetEase",
            MusicGathererIds.PlatformMigu => "Gatherer.Desc.PlatformMigu",
            MusicGathererIds.AggregatorMyFreeMp3 => "Gatherer.Desc.AggregatorMyFreeMp3",
            MusicGathererIds.AggregatorJbsou => "Gatherer.Desc.AggregatorJbsou",
            _ => "General.Unknown"
        });
}
