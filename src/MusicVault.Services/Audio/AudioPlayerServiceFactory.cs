using MusicVault.Core.Interfaces;

namespace MusicVault.Services.Audio;

internal static class AudioPlayerServiceFactory
{
    public static string LastCreatedEngineName { get; private set; } = "unknown";
    public static PlaybackEngineSnapshot? LastSnapshot { get; private set; }

    public static IAudioPlayerService Create(string dataDirectory)
    {
        var preferLibVlc = PlaybackEngineBootstrap.ReadUseLibVlcFromSettings(dataDirectory);
        var nativeRoot = LibVlcPlayerSupport.TryResolveNativeRoot();
        string? failureNote = null;

        if (preferLibVlc && LibVlcPlayerSupport.IsPackagedForCurrentPlatform)
        {
            try
            {
                var player = new LibVlcPlayerService();
                Commit(dataDirectory, "LibVLC", preferLibVlc, nativeRoot, null);
                return player;
            }
            catch (Exception ex)
            {
                failureNote = TruncateNote(ex.Message);
            }
        }
        else if (preferLibVlc)
        {
            failureNote = "LibVLC requested but not packaged for this platform";
        }

        if (OperatingSystem.IsWindows())
        {
            var note = failureNote
                ?? (preferLibVlc ? "LibVLC requested but unavailable" : null);
            Commit(dataDirectory, "NAudio", preferLibVlc, nativeRoot, note);
            return new NAudioPlayerService();
        }

        if (OperatingSystem.IsMacOS())
        {
            var note = failureNote
                ?? (preferLibVlc ? "LibVLC requested but unavailable" : null);
            Commit(dataDirectory, "AVFoundation", preferLibVlc, nativeRoot, note);
            return new MacAvFoundationPlayerService();
        }

        Commit(
            dataDirectory,
            "NAudioStub",
            preferLibVlc,
            nativeRoot,
            failureNote ?? (preferLibVlc ? "LibVLC requested but unavailable" : null));
        return new NAudioPlayerService();
    }

    private static void Commit(
        string dataDirectory,
        string engine,
        bool preferLibVlc,
        string? nativeRoot,
        string? note)
    {
        LastCreatedEngineName = engine;
        var snapshot = new PlaybackEngineSnapshot
        {
            Utc = DateTimeOffset.UtcNow,
            Engine = engine,
            PreferLibVlc = preferLibVlc,
            NativeRoot = nativeRoot ?? PlaybackEngineSnapshot.NoneNativeRoot,
            Note = note
        };
        LastSnapshot = snapshot;
        try
        {
            PlaybackEngineSnapshot.Write(dataDirectory, snapshot);
            AppendStartupLog(dataDirectory, snapshot);
        }
        catch
        {
            /* ignore */
        }
    }

    private static void AppendStartupLog(string dataDirectory, PlaybackEngineSnapshot snapshot)
    {
        try
        {
            var path = Path.Combine(dataDirectory, "playback.log");
            var line =
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [Engine] " +
                $"engine={snapshot.Engine} prefer={snapshot.PreferLibVlc.ToString().ToLowerInvariant()} " +
                $"nativeRoot={snapshot.NativeRoot}" +
                (string.IsNullOrWhiteSpace(snapshot.Note) ? "" : $" note={snapshot.Note}") +
                Environment.NewLine;
            File.AppendAllText(path, line);
        }
        catch
        {
            /* ignore */
        }
    }

    private static string TruncateNote(string message)
    {
        const int max = 240;
        var oneLine = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return oneLine.Length <= max ? oneLine : oneLine[..max] + "…";
    }
}
