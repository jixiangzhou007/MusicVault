using MusicVault.Core.Models;
using MusicVault.Core.Nas;
using MusicVault.Services.Metadata;

namespace MusicVault.Services.Library;

/// <summary>WebDAV 扫描阶段：小文件全量下载，大文件 Range 读头/尾后 TagLib 解析。</summary>
public static class WebDavTrackMetadataReader
{
    private const long MaxFullDownloadBytes = 25 * 1024 * 1024;
    private const int HeadRangeBytes = 2 * 1024 * 1024;
    private const int TailRangeBytes = 128;

    public static async Task<Track> ReadTrackAsync(
        WebDavClient webDav,
        NetworkLibraryEndpoint endpoint,
        string url,
        long sizeBytes,
        CancellationToken ct = default)
    {
        if (sizeBytes <= 0)
            return TrackMetadataHelper.CreateFromRemoteUrl(url, sizeBytes);

        try
        {
            return sizeBytes <= MaxFullDownloadBytes
                ? await ReadViaFullDownloadAsync(webDav, endpoint, url, sizeBytes, ct).ConfigureAwait(false)
                : await ReadViaRangeProbeAsync(webDav, endpoint, url, sizeBytes, ct).ConfigureAwait(false);
        }
        catch
        {
            return TrackMetadataHelper.CreateFromRemoteUrl(url, sizeBytes);
        }
    }

    private static async Task<Track> ReadViaFullDownloadAsync(
        WebDavClient webDav,
        NetworkLibraryEndpoint endpoint,
        string url,
        long sizeBytes,
        CancellationToken ct)
    {
        var ext = NormalizeExtension(url);
        var temp = Path.Combine(Path.GetTempPath(), $"mv_wdav_{Guid.NewGuid():N}{ext}");
        try
        {
            await webDav.CopyToFileAsync(endpoint, url, temp, ct).ConfigureAwait(false);
            var track = await TrackMetadataHelper.ReadFromFileAsync(temp, ct).ConfigureAwait(false);
            track.FilePath = url;
            track.FileSizeBytes = sizeBytes > 0 ? sizeBytes : track.FileSizeBytes;
            return track;
        }
        finally
        {
            TryDelete(temp);
        }
    }

    private static async Task<Track> ReadViaRangeProbeAsync(
        WebDavClient webDav,
        NetworkLibraryEndpoint endpoint,
        string url,
        long sizeBytes,
        CancellationToken ct)
    {
        var ext = NormalizeExtension(url);
        var temp = Path.Combine(Path.GetTempPath(), $"mv_wdav_rng_{Guid.NewGuid():N}{ext}");
        try
        {
            var headLen = (int)Math.Min(HeadRangeBytes, sizeBytes);
            var head = await webDav.ReadRangeAsync(endpoint, url, 0, headLen, ct).ConfigureAwait(false);

            await using (var fs = new FileStream(temp, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                if (sizeBytes > headLen)
                    fs.SetLength(sizeBytes);

                await fs.WriteAsync(head, ct).ConfigureAwait(false);

                if (ShouldFetchTail(ext, sizeBytes, headLen))
                {
                    var tailStart = Math.Max(0, sizeBytes - TailRangeBytes);
                    var tailLen = (int)Math.Min(TailRangeBytes, sizeBytes - tailStart);
                    var tail = await webDav.ReadRangeAsync(endpoint, url, tailStart, tailLen, ct)
                        .ConfigureAwait(false);
                    fs.Seek(tailStart, SeekOrigin.Begin);
                    await fs.WriteAsync(tail, ct).ConfigureAwait(false);
                }
            }

            var track = await TrackMetadataHelper.ReadFromFileAsync(temp, ct).ConfigureAwait(false);
            track.FilePath = url;
            track.FileSizeBytes = sizeBytes;
            return track;
        }
        finally
        {
            TryDelete(temp);
        }
    }

    internal static bool ShouldFetchTail(string ext, long sizeBytes, int headLen) =>
        sizeBytes > headLen
        && ext is ".mp3" or ".aac" or ".m4a" or ".mp4";

    private static string NormalizeExtension(string url)
    {
        var ext = Path.GetExtension(WebDavPathHelper.GetFileName(url));
        return string.IsNullOrWhiteSpace(ext) ? ".mp3" : ext.ToLowerInvariant();
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // ignore temp cleanup failure
        }
    }
}
