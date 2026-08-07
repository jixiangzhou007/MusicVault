using MusicVault.Core.Interfaces;
using MusicVault.Services;

namespace MusicVault.Services.Metadata;

/// <summary>写入标签前释放当前播放引擎对同路径文件的占用，并带短暂重试（NAudio / LibVLC / AVFoundation）。</summary>
public sealed class PlaybackFileWriteCoordinator : IPlaybackFileWriteCoordinator
{
    private readonly IAudioPlayerService? _player;
    private Func<string, Func<CancellationToken, Task>, CancellationToken, Task>? _appSuspend;

    public PlaybackFileWriteCoordinator(IAudioPlayerService? player = null) => _player = player;

    public void SetAppSuspendHandler(
        Func<string, Func<CancellationToken, Task>, CancellationToken, Task>? handler) =>
        _appSuspend = handler;

    public async Task RunAsync(string filePath, Func<CancellationToken, Task> action, CancellationToken ct = default)
    {
        if (_appSuspend != null)
        {
            await _appSuspend(filePath, action, ct).ConfigureAwait(false);
            return;
        }

        var released = TryReleasePlayerForPath(filePath);
        if (released)
            await Task.Delay(120, ct).ConfigureAwait(false);

        await RunWithRetryAsync(action, ct).ConfigureAwait(false);
    }

    private bool TryReleasePlayerForPath(string filePath)
    {
        if (_player == null || string.IsNullOrWhiteSpace(_player.LoadedFilePath))
            return false;

        if (!PlaybackPathComparer.AreEqual(_player.LoadedFilePath, filePath))
            return false;

        _player.Stop();
        return true;
    }

    public static async Task RunWithRetryAsync(Func<CancellationToken, Task> action, CancellationToken ct)
    {
        const int maxAttempts = 5;
        Exception? last = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await action(ct).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (IsFileLocked(ex) && attempt < maxAttempts)
            {
                last = ex;
                await Task.Delay(80 * attempt, ct).ConfigureAwait(false);
            }
        }

        if (last != null)
            throw last;

        await action(ct).ConfigureAwait(false);
    }

    public static bool IsFileLocked(Exception ex) =>
        ex is IOException or UnauthorizedAccessException
        && (ex.HResult == unchecked((int)0x80070020)
            || ex.Message.Contains("being used by another process", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("because it is being used", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("另一个程序正在使用", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("正由另一进程使用", StringComparison.OrdinalIgnoreCase));

    public static string FormatUserMessage(Exception ex) =>
        IsFileLocked(ex)
            ? ServiceMessageCodec.Key("Metadata.FileLocked")
            : ex.Message;
}
