using MusicVault.Core.Enums;
using MusicVault.Core.Interfaces;
using MusicVault.Core.Models;

namespace MusicVault.Services.Metadata;

/// <summary>通过酷狗/酷我等平台直连搜索构建艺术家资料（无需网易云 3000 端口）。</summary>
public sealed class PlatformDirectArtistProfileFetcher
{
    private readonly IReadOnlyList<IMusicSourceProvider> _providers;

    public PlatformDirectArtistProfileFetcher(IEnumerable<IMusicSourceProvider> providers)
    {
        _providers = providers
            .OrderBy(p => PlatformPriority(p.Platform))
            .ToList();
    }

    public async Task<ArtistExternalFetchResult?> FetchAsync(string artistName, CancellationToken ct = default)
    {
        foreach (var provider in _providers)
        {
            try
            {
                var tracks = await provider.SearchAsync(artistName, 1, ct).ConfigureAwait(false);
                if (tracks.Count == 0)
                    continue;

                var result = ArtistProfileResultBuilder.FromTracks(
                    tracks,
                    artistName,
                    MapPlatformLabel(provider.Platform));
                if (result != null)
                    return result;
            }
            catch
            {
                // 尝试下一平台
            }
        }

        return null;
    }

    private static int PlatformPriority(MusicPlatform platform) => platform switch
    {
        MusicPlatform.Kugou => 0,
        MusicPlatform.Kuwo => 1,
        MusicPlatform.QQMusic => 2,
        MusicPlatform.NetEase => 3,
        MusicPlatform.Migu => 4,
        _ => 9
    };

    private static string MapPlatformLabel(MusicPlatform platform) =>
        ArtistSourceKeys.ForMusicPlatform(platform);
}
