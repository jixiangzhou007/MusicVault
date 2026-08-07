using MusicVault.Core.Enums;
using MusicVault.Services;

namespace MusicVault.Services.Metadata;

/// <summary>艺术家/专辑资料来源名称 i18n key。</summary>
public static class ArtistSourceKeys
{
    public static string NetEaseMusic => ServiceMessageCodec.Key("Artist.Source.NetEaseMusic");
    public static string BaiduBaike => ServiceMessageCodec.Key("Artist.Source.BaiduBaike");
    public static string QQMusic => ServiceMessageCodec.Key("Artist.Source.QQMusic");
    public static string KugouMusic => ServiceMessageCodec.Key("Artist.Source.KugouMusic");
    public static string KuwoMusic => ServiceMessageCodec.Key("Artist.Source.KuwoMusic");
    public static string MiguMusic => ServiceMessageCodec.Key("Artist.Source.MiguMusic");
    public static string GenericPlatform => ServiceMessageCodec.Key("Artist.Source.GenericPlatform");

    public static string ForMusicPlatform(MusicPlatform platform) => platform switch
    {
        MusicPlatform.NetEase => NetEaseMusic,
        MusicPlatform.QQMusic => QQMusic,
        MusicPlatform.Kugou => KugouMusic,
        MusicPlatform.Kuwo => KuwoMusic,
        MusicPlatform.Migu => MiguMusic,
        _ => GenericPlatform
    };

    public static bool IsBaiduBaike(string? source) =>
        ServiceMessageCodec.TryGetKey(source) == "Artist.Source.BaiduBaike"
        || string.Equals(source?.Trim(), "百度百科", StringComparison.Ordinal)
        || string.Equals(source?.Trim(), "Baidu Baike", StringComparison.OrdinalIgnoreCase);

    public static bool IsNetEaseMusic(string? source) =>
        ServiceMessageCodec.TryGetKey(source) == "Artist.Source.NetEaseMusic"
        || string.Equals(source?.Trim(), "网易云音乐", StringComparison.Ordinal)
        || string.Equals(source?.Trim(), "NetEase Music", StringComparison.OrdinalIgnoreCase);

    public static bool IsQQMusic(string? source) =>
        ServiceMessageCodec.TryGetKey(source) == "Artist.Source.QQMusic"
        || string.Equals(source?.Trim(), "QQ音乐", StringComparison.Ordinal)
        || string.Equals(source?.Trim(), "QQ Music", StringComparison.OrdinalIgnoreCase);
}
