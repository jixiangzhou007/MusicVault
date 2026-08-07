using MusicVault.Core.Audio;
using MusicVault.Core.Models;
using MusicVault.Data.Repositories;
#if WINDOWS
using MusicVault.Services.Audio;
#endif
using MusicVault.Services;

namespace MusicVault.Services.Features;

public sealed record TrackExtensionCorrectionResult(bool Success, string? NewPath, string? ErrorMessage);

/// <summary>将误标为 .flac 的 MP3 重命名为 .mp3 并同步曲库。</summary>
public sealed class TrackExtensionCorrectionService
{
    private readonly TrackRepository _tracks;

    public TrackExtensionCorrectionService(TrackRepository tracks) => _tracks = tracks;

    public async Task<TrackExtensionCorrectionResult> RepairId3WrappedFlacAsync(
        Track track,
        CancellationToken ct = default)
    {
        if (!AudioFormatMislabel.IsId3WrappedFlac(track.FilePath))
            return new TrackExtensionCorrectionResult(false, null, ServiceMessageCodec.Key("Extension.NotId3WrappedFlac"));

        var oldPath = track.FilePath;
        var contentStart = AudioStreamFormatSniffer.GetLeadingMetadataSkipLength(oldPath);
        if (contentStart <= 0)
            return new TrackExtensionCorrectionResult(false, null, ServiceMessageCodec.Key("Extension.NotId3WrappedFlac"));

        var newPath = AudioFormatMislabel.ToFlacPath(oldPath);

        try
        {
            if (!File.Exists(oldPath))
                return new TrackExtensionCorrectionResult(false, null, ServiceMessageCodec.Key("Extension.SourceNotFound"));

            if (File.Exists(newPath)
                && !string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
            {
                return new TrackExtensionCorrectionResult(
                    false,
                    null,
                    ServiceMessageCodec.Format("Extension.TargetExists", Path.GetFileName(newPath)));
            }

            var tempPath = newPath + ".repair.tmp";
            await using (var input = File.OpenRead(oldPath))
            {
                input.Position = contentStart;
                await using var output = File.Create(tempPath);
                await input.CopyToAsync(output, ct).ConfigureAwait(false);
            }

            if (File.Exists(oldPath))
                File.Delete(oldPath);
            File.Move(tempPath, newPath);

            if (AudioFormatMislabel.IsId3WrappedFlac(newPath))
            {
                try { if (File.Exists(newPath)) File.Delete(newPath); } catch { /* ignore */ }
                return new TrackExtensionCorrectionResult(
                    false,
                    null,
                    ServiceMessageCodec.Key("Extension.RepairStillId3"));
            }

            var lyricsPath = track.LyricsPath;
            if (!string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
                TryRenameSidecar(oldPath, newPath, ".lrc", ref lyricsPath);

            var size = new FileInfo(newPath).Length;
            if (track.Id > 0)
                await _tracks.RenameFilePathAsync(track.Id, newPath, size, lyricsPath, ct).ConfigureAwait(false);

            track.LyricsPath = lyricsPath;
            track.FilePath = newPath;
            track.FileSizeBytes = size;

#if WINDOWS
            AudioPlaybackPathResolver.Invalidate(oldPath);
            AudioPlaybackPathResolver.Invalidate(newPath);
#endif

            return new TrackExtensionCorrectionResult(true, newPath, null);
        }
        catch (Exception ex)
        {
            try
            {
                var tempPath = newPath + ".repair.tmp";
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch { /* ignore */ }

            return new TrackExtensionCorrectionResult(false, null, ex.Message);
        }
    }

    public async Task<TrackExtensionCorrectionResult> CorrectFlacToMp3Async(
        Track track,
        CancellationToken ct = default)
    {
        if (AudioFormatMislabel.IsId3WrappedFlac(track.FilePath))
        {
            return new TrackExtensionCorrectionResult(
                false,
                null,
                ServiceMessageCodec.Key("Extension.UseRepairNotConvert"));
        }

        if (!AudioFormatMislabel.IsMp3ContentWithFlacExtension(track.FilePath))
            return new TrackExtensionCorrectionResult(false, null, ServiceMessageCodec.Key("Extension.NotMp3DisguisedAsFlac"));

        var oldPath = track.FilePath;
        var newPath = AudioFormatMislabel.ToMp3Path(oldPath);

        try
        {
            if (!File.Exists(oldPath))
                return new TrackExtensionCorrectionResult(false, null, ServiceMessageCodec.Key("Extension.SourceNotFound"));

            if (File.Exists(newPath)
                && !string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
            {
                return new TrackExtensionCorrectionResult(
                    false,
                    null,
                    ServiceMessageCodec.Format("Extension.TargetExists", Path.GetFileName(newPath)));
            }

            if (!string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
                File.Move(oldPath, newPath);

            var lyricsPath = track.LyricsPath;
            TryRenameSidecar(oldPath, newPath, ".lrc", ref lyricsPath);

            var size = new FileInfo(newPath).Length;
            if (track.Id > 0)
                await _tracks.RenameFilePathAsync(track.Id, newPath, size, lyricsPath, ct).ConfigureAwait(false);

            track.LyricsPath = lyricsPath;

            track.FilePath = newPath;
            track.FileSizeBytes = size;

            return new TrackExtensionCorrectionResult(true, newPath, null);
        }
        catch (Exception ex)
        {
            return new TrackExtensionCorrectionResult(false, null, ex.Message);
        }
    }

    private static void TryRenameSidecar(
        string oldAudioPath,
        string newAudioPath,
        string sidecarExt,
        ref string? lyricsPathField)
    {
        var oldSidecar = Path.ChangeExtension(oldAudioPath, sidecarExt);
        if (!File.Exists(oldSidecar))
            return;

        var newSidecar = Path.ChangeExtension(newAudioPath, sidecarExt);
        if (!File.Exists(newSidecar))
            File.Move(oldSidecar, newSidecar);

        if (!string.IsNullOrWhiteSpace(lyricsPathField)
            && string.Equals(lyricsPathField, oldSidecar, StringComparison.OrdinalIgnoreCase))
        {
            lyricsPathField = newSidecar;
        }
    }
}
