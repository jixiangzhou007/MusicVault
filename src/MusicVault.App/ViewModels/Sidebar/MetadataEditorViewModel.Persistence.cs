using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;
using MusicVault.Core.Models;
using MusicVault.Services.Features;
using MusicVault.Services.Metadata;
using PlaybackFileWrite = MusicVault.Services.Metadata.PlaybackFileWriteCoordinator;

namespace MusicVault.App.ViewModels.Sidebar;

public partial class MetadataEditorViewModel
{
    [RelayCommand]
    private async Task SaveAsync() =>
        await PersistToFileAsync(L10n.Get("Metadata.Status.SaveAll"));

    private async Task PersistToFileAsync(string successMessage)
    {
        if (Track == null) return;
        IsBusy = true;
        var previousTrackId = Track.Id;
        try
        {
            Track = await Services.TrackMetadataSave.SaveFullAsync(Track, CreateEditorFields());
            await PersistFavoriteAfterSaveAsync(previousTrackId);
            StatusMessage = successMessage;
            Saved?.Invoke(this, Track);
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(
                L10n.Get("Metadata.Status.WriteFailed"),
                ServiceMessageLocalizer.Resolve(L10n, PlaybackFileWrite.FormatUserMessage(ex)));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SyncFieldAsync(string? fieldKey)
    {
        if (Track == null || string.IsNullOrWhiteSpace(fieldKey))
            return;

        IsBusy = true;
        var previousTrackId = Track.Id;
        try
        {
            var label = fieldKey switch
            {
                "Lyrics" => await ApplyLyricsAsync(),
                "Cover" => await ApplyCoverAsync(),
                "Composer" or "Lyricist" or "Comment" or "Disc" => await ApplyExtendedFieldAsync(fieldKey),
                _ => await ApplyStandardTagFieldAsync(fieldKey)
            };

            TrackMetadataHelper.EnsureDuration(Track);
            Track.Id = await Services.Tracks.UpsertAsync(Track, validateArtistNames: false);
            await PersistFavoriteAfterSaveAsync(previousTrackId);
            StatusMessage = string.Format(L10n.Get("Metadata.Status.Written"), label);
            Saved?.Invoke(this, Track);
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(
                L10n.Get("Metadata.Status.WriteFailed"),
                ServiceMessageLocalizer.Resolve(L10n, PlaybackFileWrite.FormatUserMessage(ex)));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<string> ApplyStandardTagFieldAsync(string fieldKey)
    {
        var disk = await ReadDiskTagsForWriteAsync();
        ApplyEditorStandardTagsToDisk(disk);
        await Services.Metadata.WriteTagsAsync(disk);
        CopyDiskTagsToTrack(disk);

        return fieldKey switch
        {
            "Title" => L10n.Get("Metadata.Label.Title"),
            "Artist" => L10n.Get("Metadata.Label.Artist"),
            "Album" => L10n.Get("Metadata.Label.Album"),
            "AlbumArtist" => L10n.Get("Metadata.Label.AlbumArtist"),
            "TrackNumber" => L10n.Get("Metadata.Label.TrackNumber"),
            "Year" => L10n.Get("Metadata.Label.Year"),
            "Genre" => L10n.Get("Metadata.Label.Genre"),
            _ => throw new InvalidOperationException($"Unknown field: {fieldKey}")
        };
    }

    private async Task<string> ApplyExtendedFieldAsync(string fieldKey)
    {
        var (composer, comment, disc, lyricist) = await Services.Metadata.ReadExtendedTagsAsync(Track!.FilePath);
        var label = fieldKey switch
        {
            "Composer" => ApplyComposer(ref composer),
            "Lyricist" => ApplyLyricist(ref lyricist),
            "Comment" => ApplyComment(ref comment),
            "Disc" => ApplyDisc(ref disc),
            _ => throw new InvalidOperationException($"Unknown field: {fieldKey}")
        };

        await Services.Metadata.WriteExtendedTagsAsync(Track.FilePath, composer, comment, disc, lyricist);
        return label;
    }

    private async Task<Track> ReadDiskTagsForWriteAsync()
    {
        var disk = await Services.Metadata.ReadTagsAsync(Track!.FilePath);
        disk.FilePath = Track.FilePath;
        if (Track.Id > 0)
            disk.Id = Track.Id;
        return disk;
    }

    private void ApplyEditorStandardTagsToDisk(Track disk)
    {
        disk.Title = Title.Trim();
        disk.Artist = Artist.Trim();
        disk.Album = Album.Trim();
        disk.AlbumArtist = AlbumArtist.Trim();
        disk.TrackNumber = int.TryParse(TrackNumberText, out var tn) && tn > 0 ? tn : null;
        disk.Year = int.TryParse(YearText, out var y) && y > 0 ? y : null;
        disk.Genre = Genre.Trim();
    }

    private string ApplyComposer(ref string composer)
    {
        composer = Composer.Trim();
        return L10n.Get("Metadata.Label.Composer");
    }

    private string ApplyLyricist(ref string lyricist)
    {
        lyricist = Lyricist.Trim();
        return L10n.Get("Metadata.Label.Lyricist");
    }

    private string ApplyComment(ref string comment)
    {
        comment = Comment.Trim();
        return L10n.Get("Metadata.Label.Comment");
    }

    private string ApplyDisc(ref string disc)
    {
        disc = DiscNumberText.Trim();
        return L10n.Get("Metadata.Label.DiscNumber");
    }

    private async Task<string> ApplyLyricsAsync()
    {
        if (string.IsNullOrWhiteSpace(LyricsText))
        {
            await Services.Metadata.ClearLyricsAsync(Track!.FilePath);
            Track.LyricsPath = null;
            return L10n.Get("Metadata.Label.LyricsCleared");
        }

        await Services.Metadata.EmbedLyricsAsync(Track!.FilePath, LyricsText);
        Track.LyricsPath = Path.ChangeExtension(Track.FilePath, ".lrc");
        return L10n.Get("Metadata.Label.Lyrics");
    }

    private async Task<string> ApplyCoverAsync()
    {
        if (CoverImage is not { Length: > 0 })
        {
            await Services.Metadata.ClearCoverAsync(Track!.FilePath);
            return L10n.Get("Metadata.Label.CoverCleared");
        }

        await Services.Metadata.EmbedCoverAsync(Track!.FilePath, CoverImage);
        return L10n.Get("Metadata.Label.Cover");
    }

    private void CopyDiskTagsToTrack(Track disk)
    {
        if (Track == null) return;
        Track.Title = disk.Title;
        Track.Artist = disk.Artist;
        Track.Album = disk.Album;
        Track.AlbumArtist = disk.AlbumArtist;
        Track.TrackNumber = disk.TrackNumber;
        Track.Year = disk.Year;
        Track.Genre = disk.Genre;
    }

    private void ApplyFieldsToTrack(Track track)
    {
        track.Title = Title.Trim();
        track.Artist = Artist.Trim();
        track.Album = Album.Trim();
        track.AlbumArtist = AlbumArtist.Trim();
        track.TrackNumber = int.TryParse(TrackNumberText, out var tn) && tn > 0 ? tn : null;
        track.Year = int.TryParse(YearText, out var y) && y > 0 ? y : null;
        track.Genre = Genre.Trim();
    }

    private TrackMetadataEditorFields CreateEditorFields() => new(
        Title,
        Artist,
        Album,
        AlbumArtist,
        TrackNumberText,
        YearText,
        Genre,
        Composer,
        Comment,
        DiscNumberText,
        Lyricist,
        LyricsText,
        CoverImage);

    [RelayCommand]
    private void OpenFileLocation()
    {
        if (Track == null || string.IsNullOrWhiteSpace(Track.FilePath)) return;
        Services.Platform.RevealInFileManager(Track.FilePath);
    }
}
