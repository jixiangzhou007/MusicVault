using MusicVault.Core.Models;
using MusicVault.Services.Metadata;

namespace MusicVault.Services.Features;

/// <summary>艺术家详情：缓存读取、网络刷新、专辑目录构建（防竞态、可单测）。</summary>
public sealed class ArtistDetailRefreshService
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

    private readonly ArtistProfileService _profiles;

    public ArtistDetailRefreshService(ArtistProfileService profiles) =>
        _profiles = profiles;

    public Task<ArtistProfile?> GetCachedProfileAsync(string artistName, CancellationToken ct = default) =>
        _profiles.GetCachedAsync(artistName, ct);

    public Task<IReadOnlyList<OnlineArtistAlbum>> GetCachedOfficialAlbumsAsync(
        string artistName,
        CancellationToken ct = default) =>
        _profiles.GetCachedAlbumsAsync(artistName, ct);

    public Task<ArtistProfile> RefreshProfileFromNetworkAsync(
        string artistName,
        ArtistProfile? existing,
        CancellationToken ct = default) =>
        _profiles.RefreshFromBaiduAsync(artistName, existing, ct, enrichBaidu: true);

    public async Task<IReadOnlyList<OnlineArtistAlbum>> FetchOfficialAlbumsAsync(
        ArtistProfile? profile,
        CancellationToken ct = default)
    {
        if (profile == null)
            return [];

        return await _profiles.FetchOfficialAlbumsAsync(profile, ct).ConfigureAwait(false);
    }

    public IReadOnlyList<ArtistAlbumCatalogItem> BuildCatalog(
        IReadOnlyList<Track> libraryTracks,
        string artistName,
        IReadOnlyList<OnlineArtistAlbum> officialOnline) =>
        ArtistAlbumCatalogBuilder.Build(libraryTracks, artistName, officialOnline);

    /// <summary>
    /// 刷新资料：先推送库内专辑目录，再并行拉简介与在线专辑；<paramref name="progress"/> 可在各阶段先完成先回调。
    /// </summary>
    public async Task<ArtistDetailRefreshResult> RefreshAsync(
        ArtistDetailRefreshRequest request,
        ArtistDetailRefreshProgress? progress = null,
        CancellationToken ct = default)
    {
        var libraryOnly = BuildCatalog(request.LibraryTracks, request.ArtistName, []);
        var libraryHint = ArtistAlbumCatalogBuilder.ResolveSourceHint([]);
        progress?.OnAlbumsReady?.Invoke(libraryOnly, libraryHint);

        var existing = request.ExistingProfile;
        var profileTask = RefreshProfileFromNetworkAsync(request.ArtistName, existing, ct);
        var albumsTask = FetchOfficialAlbumsAsync(existing, ct);

        ArtistProfile? profile = null;
        IReadOnlyList<OnlineArtistAlbum> official = [];
        var profileDone = false;
        var albumsNetworkDone = false;

        while (!profileDone || !albumsNetworkDone)
        {
            var tasks = new List<Task>();
            if (!profileDone)
                tasks.Add(profileTask);
            if (!albumsNetworkDone)
                tasks.Add(albumsTask);

            var completed = await Task.WhenAny(tasks).ConfigureAwait(false);

            if (ReferenceEquals(completed, profileTask))
            {
                profile = await profileTask.ConfigureAwait(false);
                profileDone = true;
                progress?.OnProfileReady?.Invoke(profile);
                if (ct.IsCancellationRequested)
                {
                    return new ArtistDetailRefreshResult(
                        profile,
                        libraryOnly,
                        libraryHint,
                        ProfileCompleted: true,
                        AlbumsCompleted: albumsNetworkDone);
                }
            }
            else
            {
                official = await albumsTask.ConfigureAwait(false);
                albumsNetworkDone = true;
                if (official.Count > 0)
                    await _profiles.SaveAlbumsCacheAsync(request.ArtistName, official, ct).ConfigureAwait(false);
                NotifyAlbumsReady(request, official, progress);
            }
        }

        profile ??= await profileTask.ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(profile.LemmaId)
            && profile.LemmaId != existing?.LemmaId
            && official.Count == 0)
        {
            official = await FetchOfficialAlbumsAsync(profile, ct).ConfigureAwait(false);
            if (official.Count > 0)
                await _profiles.SaveAlbumsCacheAsync(request.ArtistName, official, ct).ConfigureAwait(false);
            NotifyAlbumsReady(request, official, progress);
        }

        var merged = BuildCatalog(request.LibraryTracks, request.ArtistName, official);
        var hint = ArtistAlbumCatalogBuilder.ResolveSourceHint(official);
        return new ArtistDetailRefreshResult(profile, merged, hint, true, true);
    }

    private void NotifyAlbumsReady(
        ArtistDetailRefreshRequest request,
        IReadOnlyList<OnlineArtistAlbum> official,
        ArtistDetailRefreshProgress? progress)
    {
        if (progress?.OnAlbumsReady == null)
            return;

        var merged = BuildCatalog(request.LibraryTracks, request.ArtistName, official);
        progress.OnAlbumsReady.Invoke(merged, ArtistAlbumCatalogBuilder.ResolveSourceHint(official));
    }

    public byte[]? LoadPhotoBytes(ArtistProfile? profile) =>
        _profiles.LoadPhotoBytes(profile);
}

public sealed record ArtistDetailRefreshRequest(
    string ArtistName,
    IReadOnlyList<Track> LibraryTracks,
    ArtistProfile? ExistingProfile);

public sealed record ArtistDetailRefreshResult(
    ArtistProfile Profile,
    IReadOnlyList<ArtistAlbumCatalogItem> AlbumItems,
    string AlbumsSourceHint,
    bool ProfileCompleted,
    bool AlbumsCompleted);

/// <summary>艺术家详情刷新进度（简介/专辑谁先完成谁先通知 UI）。</summary>
public sealed class ArtistDetailRefreshProgress
{
    public Action<ArtistProfile>? OnProfileReady { get; init; }
    public Action<IReadOnlyList<ArtistAlbumCatalogItem>, string>? OnAlbumsReady { get; init; }
}
