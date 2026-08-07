using MusicVault.Core.Enums;
using MusicVault.Core.Models;

namespace MusicVault.Services.Features;

/// <summary>艺术家详情页专辑行（与 UI ViewModel 解耦）。</summary>
public sealed record ArtistAlbumCatalogItem(
    string Album,
    int LibraryTrackCount,
    int? Year,
    MusicPlatform? Platform,
    string? PlatformAlbumId,
    Track? LibrarySample,
    string? OnlinePicUrl);
