namespace MusicVault.Core.Gatherers;

/// <summary>内置采集源标识（写入 settings.json，便于后续管理页扩展）</summary>
public static class MusicGathererIds
{
    public const string UnifiedGoMusicApi = "unified-go-music-api";
    public const string NetEaseApi = "netease-api";
    public const string QqMusicApi = "qq-music-api";
    public const string PlatformKugou = "platform-kugou";
    public const string PlatformKuwo = "platform-kuwo";
    public const string PlatformQq = "platform-qq";
    public const string PlatformNetEase = "platform-netease";
    public const string PlatformMigu = "platform-migu";
    public const string AggregatorMyFreeMp3 = "aggregator-myfreemp3";
    public const string AggregatorJbsou = "aggregator-jbsou";
}
