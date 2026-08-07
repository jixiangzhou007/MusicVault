namespace MusicVault.Core.Enums;

/// <summary>支持的在线音乐平台</summary>
public enum MusicPlatform
{
    NetEase = 0,
    QQMusic = 1,
    Kugou = 2,
    Kuwo = 3,
    Migu = 4,
    /// <summary>第三方聚合站 MyFreeMP3</summary>
    MyFreeMp3 = 5,
    /// <summary>第三方聚合站 JBSou（jbsou.cn）</summary>
    Jbsou = 6,
    /// <summary>汽水音乐（go-music-api source: soda）</summary>
    Soda = 7,
    /// <summary>远程 NAS 库复制到本地</summary>
    NasLibrary = 8,
    All = 99
}
