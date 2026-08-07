using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;
using MusicVault.Core.Audio;
using MusicVault.Core.Models;
using MusicVault.Services.Metadata;

namespace MusicVault.App.ViewModels.Sidebar;

public partial class MetadataEditorViewModel
{
    partial void OnIsOnlineSourceModeChanged(bool value)
    {
        if (!value && Track != null && string.IsNullOrWhiteSpace(MetadataSourceFilePath))
            MetadataSourceFilePath = Track.FilePath;
    }

    [RelayCommand]
    private async Task ReadFromSourceFileAsync()
    {
        if (!IsFromFileSourceMode) return;
        if (string.IsNullOrWhiteSpace(MetadataSourceFilePath))
        {
            StatusMessage = L10n.Get("Metadata.Status.SelectFileFirst");
            return;
        }

        if (!File.Exists(MetadataSourceFilePath))
        {
            StatusMessage = L10n.Get("Metadata.Status.FileNotFound");
            return;
        }

        IsBusy = true;
        StatusMessage = L10n.Get("Metadata.Status.Reading");
        try
        {
            await LoadFieldsFromFileAsync(MetadataSourceFilePath, mergeWithCurrentFields: false, LoadGeneration);
            StatusMessage = L10n.Get("Metadata.Status.ReadFromFile");
        }
        catch (Exception ex)
        {
            StatusMessage = ServiceMessageLocalizer.Format(L10n, "Metadata.Status.ReadFailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PickMetadataSourceFileAsync()
    {
        if (Application.Current?.ApplicationLifetime
            is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        var files = await desktop.MainWindow!.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = L10n.Get("Metadata.Status.PickAudioTitle"),
                AllowMultiple = false,
                FileTypeFilter = [BuildAudioFilePickerFilter()]
            });

        var file = files.FirstOrDefault();
        if (file == null) return;

        MetadataSourceFilePath = file.Path.LocalPath;
        StatusMessage = L10n.Get("Metadata.Status.FileSelected");
    }

    private FilePickerFileType BuildAudioFilePickerFilter()
    {
        var patterns = AudioFormatCatalog.DefaultPlaybackExtensions
            .Select(ext => ext.StartsWith('.') ? $"*{ext}" : $"*.{ext}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new FilePickerFileType(L10n.Get("Metadata.Status.PickAudioType")) { Patterns = patterns };
    }

    private async Task LoadFieldsFromFileAsync(string filePath, bool mergeWithCurrentFields, int expectedGeneration)
    {
        var fromFile = await TrackMetadataHelper.ReadFromFileAsync(filePath);
        var (composer, comment, disc, lyricist) = await Services.Metadata.ReadExtendedTagsAsync(filePath);
        var coverDetails = await Services.Metadata.ReadEmbeddedCoverDetailsAsync(filePath);
        var lyrics = await Services.Metadata.ReadLyricsAsync(filePath);

        await RunOnUiAsync(() =>
        {
            if (LoadGeneration != expectedGeneration || Track == null)
                return;

            if (!PlaybackPathComparer.AreEqual(Track.FilePath, filePath))
                return;

            if (mergeWithCurrentFields)
            {
                if (!string.IsNullOrWhiteSpace(fromFile.Title))
                    Title = fromFile.Title.Trim();
                if (!string.IsNullOrWhiteSpace(fromFile.Artist))
                    Artist = fromFile.Artist.Trim();
                if (!string.IsNullOrWhiteSpace(fromFile.Album))
                    Album = fromFile.Album.Trim();
                if (!string.IsNullOrWhiteSpace(fromFile.AlbumArtist))
                    AlbumArtist = fromFile.AlbumArtist.Trim();
                if (fromFile.TrackNumber is > 0)
                    TrackNumberText = fromFile.TrackNumber.Value.ToString();
                if (fromFile.Year is > 0)
                    YearText = fromFile.Year.Value.ToString();
                if (!string.IsNullOrWhiteSpace(fromFile.Genre))
                {
                    Genre = MetadataGenreHeuristic.Sanitize(fromFile.Genre.Trim(), Title, LyricsText);
                    EnsureGenreOption(Genre);
                }
            }
            else
            {
                Title = fromFile.Title.Trim();
                Artist = fromFile.Artist.Trim();
                Album = fromFile.Album.Trim();
                AlbumArtist = fromFile.AlbumArtist.Trim();
                TrackNumberText = fromFile.TrackNumber?.ToString() ?? string.Empty;
                YearText = fromFile.Year?.ToString() ?? string.Empty;
                Genre = MetadataGenreHeuristic.Sanitize(fromFile.Genre.Trim(), Title, LyricsText);
                EnsureGenreOption(Genre);
            }

            Composer = composer;
            Lyricist = lyricist;
            Comment = comment;
            DiscNumberText = disc;
            if (coverDetails != null)
                SetCoverImage(coverDetails.Value.Data, coverDetails.Value.MimeType, coverDetails.Value.PictureTypeLabel);
            else
                SetCoverImage(null);
            LyricsText = lyrics;
            if (!string.IsNullOrWhiteSpace(Genre)
                && !MetadataGenreHeuristic.IsPlausible(Genre, Title, lyrics))
                Genre = string.Empty;
        });
    }

    private static async Task RunOnUiAsync(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            await Dispatcher.UIThread.InvokeAsync(action);
    }
}
