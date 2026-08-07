using MusicVault.Core.Enums;

namespace MusicVault.Services;

/// <summary>平台名称 i18n Key（供服务层 diagnostics 编码）。</summary>
public static class ServicePlatformKeys
{
    public static string Key(MusicPlatform platform) => platform switch
    {
        MusicPlatform.NetEase => "Platform.NetEase",
        MusicPlatform.QQMusic => "Platform.QQMusic",
        MusicPlatform.Kugou => "Platform.Kugou",
        MusicPlatform.Kuwo => "Platform.Kuwo",
        MusicPlatform.Migu => "Platform.Migu",
        MusicPlatform.MyFreeMp3 => "Platform.MyFreeMp3",
        MusicPlatform.Jbsou => "Platform.Jbsou",
        MusicPlatform.Soda => "Platform.Soda",
        _ => "Platform.Unknown"
    };
}
