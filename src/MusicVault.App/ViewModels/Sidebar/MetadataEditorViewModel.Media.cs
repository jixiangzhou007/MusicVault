using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;
using MusicVault.App.Metadata;
using MusicVault.App.ViewModels.Dialogs;

namespace MusicVault.App.ViewModels.Sidebar;

public partial class MetadataEditorViewModel
{
    [RelayCommand]
    private async Task PickCoverFromFileAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        var files = await desktop.MainWindow!.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = L10n.Get("Metadata.Status.PickCoverTitle"),
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType(L10n.Get("Metadata.Status.PickCoverType")) { Patterns = ["*.jpg", "*.jpeg", "*.png", "*.webp", "*.gif"] }
                ]
            });

        var file = files.FirstOrDefault();
        if (file == null)
            return;

        await using var stream = await file.OpenReadAsync();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        var bytes = ms.ToArray();
        if (bytes.Length == 0)
            return;

        var mime = CoverInfoHelper.GuessMime(bytes);
        await RunOnUiAsync(() =>
        {
            SetCoverImage(bytes, mime);
            StatusMessage = L10n.Get("Metadata.Status.CoverLoaded");
        });
    }

    [RelayCommand]
    private async Task ReadCoverFromTagsAsync()
    {
        var path = !string.IsNullOrWhiteSpace(MetadataSourceFilePath)
            ? MetadataSourceFilePath
            : Track?.FilePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            StatusMessage = L10n.Get("Metadata.Status.NoAudioForCover");
            return;
        }

        IsBusy = true;
        try
        {
            var details = await Services.Metadata.ReadEmbeddedCoverDetailsAsync(path);
            await RunOnUiAsync(() =>
            {
                if (details == null)
                {
                    SetCoverImage(null);
                    StatusMessage = L10n.Get("Metadata.Status.NoEmbeddedCover");
                    return;
                }

                SetCoverImage(details.Value.Data, details.Value.MimeType, details.Value.PictureTypeLabel);
                StatusMessage = L10n.Get("Metadata.Status.CoverReadFromTags");
            });
        }
        catch (Exception ex)
        {
            StatusMessage = ServiceMessageLocalizer.Format(L10n, "Metadata.Status.CoverReadFailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task ClearCoverPreviewAsync()
    {
        SetCoverImage(null);
        StatusMessage = L10n.Get("Metadata.Status.CoverCleared");
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ExportCoverAsync()
    {
        if (CoverImage is not { Length: > 0 } cover)
        {
            StatusMessage = L10n.Get("Metadata.Status.NoCoverToExport");
            return;
        }

        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        var ext = CoverInfoHelper.GuessExtension(cover);
        var file = await desktop.MainWindow!.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = L10n.Get("Metadata.Status.ExportCoverTitle"),
                SuggestedFileName = $"cover{ext}",
                FileTypeChoices =
                [
                    new FilePickerFileType(L10n.Get("Metadata.Status.PickCoverType")) { Patterns = ["*.jpg", "*.jpeg", "*.png", "*.webp", "*.gif"] }
                ]
            });

        if (file == null)
            return;

        await using var stream = await file.OpenWriteAsync();
        await stream.WriteAsync(cover);
        StatusMessage = L10n.Get("Metadata.Status.CoverExported");
    }

    [RelayCommand]
    private async Task OpenCoverAsync()
    {
        if (CoverImage is not { Length: > 0 } cover)
        {
            StatusMessage = L10n.Get("Metadata.Status.NoCoverToOpen");
            return;
        }

        try
        {
            var ext = CoverInfoHelper.GuessExtension(cover);
            var temp = Path.Combine(Path.GetTempPath(), $"musicvault-cover-{Guid.NewGuid():N}{ext}");
            await File.WriteAllBytesAsync(temp, cover);
            Process.Start(new ProcessStartInfo(temp) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StatusMessage = ServiceMessageLocalizer.Format(L10n, "Metadata.Status.OpenCoverFailed", ex.Message);
        }
    }

    [RelayCommand]
    private async Task ReadLyricsFromTagsAsync()
    {
        var path = !string.IsNullOrWhiteSpace(MetadataSourceFilePath)
            ? MetadataSourceFilePath
            : Track?.FilePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            StatusMessage = L10n.Get("Metadata.Status.NoAudioForLyrics");
            return;
        }

        IsBusy = true;
        try
        {
            var lyrics = await Services.Metadata.ReadLyricsAsync(path);
            await RunOnUiAsync(() =>
            {
                LyricsText = lyrics;
                StatusMessage = string.IsNullOrWhiteSpace(lyrics)
                    ? L10n.Get("Metadata.Status.NoEmbeddedLyrics")
                    : L10n.Get("Metadata.Status.LyricsReadFromFile");
            });
        }
        catch (Exception ex)
        {
            StatusMessage = ServiceMessageLocalizer.Format(L10n, "Metadata.Status.LyricsReadFailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task EditLyricsAsync()
    {
        if (_formDrawerHost == null)
        {
            StatusMessage = L10n.Get("Metadata.Status.LyricsEditorFailed");
            return;
        }

        var defaultPath = Track != null ? Path.ChangeExtension(Track.FilePath, ".lrc") : null;
        var vm = new MetadataLyricsEditorModalViewModel(_formDrawerHost, LyricsText, L10n, defaultPath);
        var result = await _formDrawerHost.ShowMetadataLyricsEditorAsync(vm);
        if (result == null || result.Action == MetadataLyricsEditorAction.Cancel)
            return;

        LyricsText = result.Lyrics;

        if (result.Action == MetadataLyricsEditorAction.ApplyAndSave)
            await SyncFieldAsync("Lyrics");
        else if (result.Action == MetadataLyricsEditorAction.SaveAsLrc)
            StatusMessage = L10n.Get("Metadata.Status.LyricsSavedLrc");
        else
            StatusMessage = L10n.Get("Metadata.Status.LyricsUpdated");
    }

    [RelayCommand]
    private async Task FixEncodingAsync(string? fieldKey)
    {
        if (_modalHost == null || string.IsNullOrWhiteSpace(fieldKey))
            return;

        var source = fieldKey switch
        {
            "Title" => Title,
            "Artist" => Artist,
            "Album" => Album,
            "AlbumArtist" => AlbumArtist,
            "Composer" => Composer,
            "Lyricist" => Lyricist,
            "Comment" => Comment,
            "Lyrics" => LyricsText,
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(source))
            return;

        var candidates = MetadataEncodingFixHelper.GetCandidates(source);
        if (candidates.Count == 0)
        {
            StatusMessage = L10n.Get("Metadata.Status.NoEncodingFix");
            return;
        }

        var vm = new MetadataEncodingFixModalViewModel(_modalHost, L10n, source, candidates);
        var result = await _modalHost.ShowMetadataEncodingFixAsync(vm);
        if (result?.Applied != true || string.IsNullOrEmpty(result.SelectedText))
            return;

        switch (fieldKey)
        {
            case "Title": Title = result.SelectedText; break;
            case "Artist": Artist = result.SelectedText; break;
            case "Album": Album = result.SelectedText; break;
            case "AlbumArtist": AlbumArtist = result.SelectedText; break;
            case "Composer": Composer = result.SelectedText; break;
            case "Lyricist": Lyricist = result.SelectedText; break;
            case "Comment": Comment = result.SelectedText; break;
            case "Lyrics": LyricsText = result.SelectedText; break;
        }

        StatusMessage = L10n.Get("Metadata.Status.EncodingFixed");
    }
}
