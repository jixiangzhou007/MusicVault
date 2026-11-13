using System.Collections.Concurrent;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using MusicVault.Core.Enums;

namespace MusicVault.App.Constants;

/// <summary>各音乐平台应用图标（avares 资源，缓存 Bitmap）。</summary>
public static class MusicPlatformIcons
{
    private static readonly ConcurrentDictionary<string, Bitmap> Cache = new(StringComparer.Ordinal);

    public static Bitmap ForPlatform(MusicPlatform platform, bool color = true) =>
        Get(FileKeyFor(platform), color);

    public static Bitmap ForUnifiedApi(bool color = true) => Get("api", color);

    public static Bitmap ForLoginStatus(MusicPlatform platform, bool isLoggedIn) =>
        platform == MusicPlatform.All
            ? ForUnifiedApi(color: isLoggedIn)
            : ForPlatform(platform, color: isLoggedIn);

    private static Bitmap Get(string fileKey, bool color) =>
        Cache.GetOrAdd(color ? fileKey : fileKey + ":gray", key =>
        {
            var stem = color ? fileKey : fileKey + "-gray";
            // 灰度资源缺失时回退彩色（避免发布遗漏导致崩溃）
            var uri = new Uri($"avares://MusicVault/Assets/Platforms/{stem}.png");
            try
            {
                using var stream = AssetLoader.Open(uri);
                return new Bitmap(stream);
            }
            catch when (!color)
            {
                using var stream = AssetLoader.Open(new Uri($"avares://MusicVault/Assets/Platforms/{fileKey}.png"));
                return new Bitmap(stream);
            }
        });

    private static string FileKeyFor(MusicPlatform platform) =>
        platform switch
        {
            MusicPlatform.All => "all",
            MusicPlatform.QQMusic => "qq",
            MusicPlatform.NetEase => "netease",
            MusicPlatform.Kugou => "kugou",
            MusicPlatform.Kuwo => "kuwo",
            MusicPlatform.Migu => "migu",
            MusicPlatform.Soda => "soda",
            MusicPlatform.MyFreeMp3 => "myfreemp3",
            MusicPlatform.Jbsou => "jbsou",
            _ => "all"
        };
}
