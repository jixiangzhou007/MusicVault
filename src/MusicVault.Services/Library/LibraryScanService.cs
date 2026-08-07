using MusicVault.Core.Audio;
using MusicVault.Core.Diagnostics;
using MusicVault.Core.Enums;
using MusicVault.Core.Interfaces;
using MusicVault.Core.Models;
using MusicVault.Core.Nas;
using MusicVault.Data.Repositories;
using MusicVault.Services.Metadata;

namespace MusicVault.Services.Library;

/// <summary>
/// 音乐库扫描服务：支持全量/增量扫描、后台进度报告、移除已删除文件索引。
/// </summary>
public sealed class LibraryScanService
{
    private readonly IMetadataService _metadata;
    private readonly TrackRepository _tracks;
    private readonly LibraryRepository _libraries;
    private readonly WebDavClient _webDav;
    private readonly WebDavLibraryService _webDavLibraries;

    private CancellationTokenSource? _backgroundCts;

    public event EventHandler<ScanProgressEventArgs>? ProgressChanged;
    public bool IsScanning { get; private set; }

    public LibraryScanService(
        TrackRepository tracks,
        LibraryRepository libraries,
        IMetadataService metadata,
        WebDavClient webDav,
        WebDavLibraryService webDavLibraries)
    {
        _tracks = tracks;
        _libraries = libraries;
        _metadata = metadata;
        _webDav = webDav;
        _webDavLibraries = webDavLibraries;
    }

    /// <summary>扫描音乐库：统一路径、认领归属、合并重复索引。</summary>
    public async Task<LibraryScanResult> ScanLibraryAsync(long libraryId, bool incremental = true, CancellationToken ct = default)
    {
        var libs = await _libraries.GetAllAsync(ct);
        var lib = libs.FirstOrDefault(l => l.Id == libraryId);
        if (lib == null)
            return new LibraryScanResult { RootPath = string.Empty, RootMissing = true };

        if (lib.SourceType == LibrarySourceType.NetworkWebDav)
            return await ScanWebDavLibraryAsync(lib, libraryId, incremental, libs, ct).ConfigureAwait(false);

        var root = LibraryRepository.NormalizeRootPath(lib.RootPath);
        if (!Directory.Exists(root))
        {
            var dbCount = await _tracks.CountByLibraryScopeAsync(libraryId, root, ct).ConfigureAwait(false);
            return new LibraryScanResult
            {
                RootPath = root,
                RootMissing = true,
                DbTrackCount = dbCount,
                IsNetworkNas = lib.SourceType == LibrarySourceType.NetworkNas
            };
        }

        IsScanning = true;
        try
        {
            await _tracks.AssignOrphansUnderRootAsync(libraryId, root, ct);

            var rawIndex = await _tracks.GetFileIndexAsync(libraryId, ct);
            var normalizedIndex = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            foreach (var (path, size) in rawIndex)
                normalizedIndex[TrackPathNormalizer.Normalize(path)] = size;

            HashSet<string>? missingAcoustIdPaths = null;
            if (AcoustIdLookupConfiguration.Enabled)
                missingAcoustIdPaths = await _tracks.GetPathsMissingAcoustIdAsync(libraryId, ct);

            var extensions = AudioFormatCatalog.MergeScanExtensions(lib.ScanExtensions);

            var files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(f => extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .Select(TrackPathNormalizer.Normalize)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var scannedNormalized = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);
            var processed = 0;
            var skipped = 0;
            var imported = 0;
            var failed = 0;
            var acoustIdLookupAttempts = 0;
            var acoustIdLookupMatches = 0;

            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    if (!File.Exists(file)) continue;

                    var fileInfo = new FileInfo(file);
                    var sizeUnchanged = incremental
                        && normalizedIndex.TryGetValue(file, out var oldSize)
                        && oldSize > 0
                        && oldSize == fileInfo.Length;
                    var needsAcoustIdBackfill = missingAcoustIdPaths != null
                        && missingAcoustIdPaths.Contains(file);
                    if (sizeUnchanged && !needsAcoustIdBackfill)
                    {
                        skipped++;
                        processed++;
                        ProgressChanged?.Invoke(this, new ScanProgressEventArgs(
                            processed, files.Count, file, skipped, acoustIdLookupAttempts, acoustIdLookupMatches));
                        continue;
                    }

                    var track = await TrackMetadataHelper.ReadFromFileAsync(file, ct);
                    track.LibraryId = libraryId;
                    track.FilePath = file;
                    track.FileSizeBytes = fileInfo.Length;
                    TrackMetadataHelper.EnsureDuration(track);
                    if (string.IsNullOrWhiteSpace(track.AcoustId)
                        && AcoustIdLookupConfiguration.Enabled)
                    {
                        acoustIdLookupAttempts++;
                        var acoustId = await _metadata.LookupByFingerprintAsync(file, ct);
                        if (!string.IsNullOrWhiteSpace(acoustId))
                        {
                            track.AcoustId = acoustId;
                            acoustIdLookupMatches++;
                        }
                    }

                    await _tracks.UpsertAsync(track, ct, validateArtistNames: false);
                    imported++;
                }
                catch
                {
                    failed++;
                }

                processed++;
                ProgressChanged?.Invoke(this, new ScanProgressEventArgs(
                    processed, files.Count, file, skipped, acoustIdLookupAttempts, acoustIdLookupMatches));
            }

            var toRemove = new List<string>();
            foreach (var (dbPath, _) in rawIndex)
            {
                var norm = TrackPathNormalizer.Normalize(dbPath);
                if (scannedNormalized.Contains(norm)) continue;
                if (File.Exists(norm) || File.Exists(dbPath)) continue;
                toRemove.Add(dbPath);
            }

            if (toRemove.Count > 0)
                await _tracks.RemoveByPathsAsync(toRemove, ct);

            var claimed = await _tracks.ClaimTracksUnderRootForScanAsync(libraryId, root, libs, ct);
            var dupes = await _tracks.RemoveDuplicateNormalizedPathsAsync(libraryId, ct);
            await EnrichMetadataFromDiskAsync(libraryId, root, ct);
            var dbCount = await _tracks.CountByLibraryScopeAsync(libraryId, root, ct);

            return new LibraryScanResult
            {
                RootPath = root,
                DiskFileCount = files.Count,
                ImportedCount = imported,
                FailedCount = failed,
                DbTrackCount = dbCount,
                ClaimedCount = claimed,
                DuplicatesRemoved = dupes,
                AcoustIdLookupAttempts = acoustIdLookupAttempts,
                AcoustIdLookupMatches = acoustIdLookupMatches
            };
        }
        finally { IsScanning = false; }
    }

    private async Task<LibraryScanResult> ScanWebDavLibraryAsync(
        MusicLibrary lib,
        long libraryId,
        bool incremental,
        IReadOnlyList<MusicLibrary> libs,
        CancellationToken ct)
    {
        var root = LibraryRepository.NormalizeRootPath(lib.RootPath);
        var endpoint = await _webDavLibraries.ResolveStoredEndpointAsync(libraryId, ct).ConfigureAwait(false);
        if (endpoint == null)
        {
            var dbCount = await _tracks.CountByLibraryScopeAsync(libraryId, root, ct).ConfigureAwait(false);
            return new LibraryScanResult
            {
                RootPath = root,
                RootMissing = true,
                DbTrackCount = dbCount,
                IsNetworkWebDav = true
            };
        }

        var test = await _webDav.TestAsync(endpoint, ct).ConfigureAwait(false);
        if (!test.Success)
        {
            var dbCount = await _tracks.CountByLibraryScopeAsync(libraryId, root, ct).ConfigureAwait(false);
            return new LibraryScanResult
            {
                RootPath = root,
                RootMissing = true,
                DbTrackCount = dbCount,
                IsNetworkWebDav = true
            };
        }

        IsScanning = true;
        try
        {
            await _tracks.AssignOrphansUnderRootAsync(libraryId, root, ct);

            var rawIndex = await _tracks.GetFileIndexAsync(libraryId, ct);
            var normalizedIndex = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            foreach (var (path, size) in rawIndex)
                normalizedIndex[TrackPathNormalizer.Normalize(path)] = size;

            var extensions = AudioFormatCatalog.MergeScanExtensions(lib.ScanExtensions);
            var remoteFiles = await _webDav.ListAudioFilesAsync(endpoint, extensions, ct).ConfigureAwait(false);
            var sizeByPath = remoteFiles.ToDictionary(
                e => TrackPathNormalizer.Normalize(e.Url),
                e => e.SizeBytes,
                StringComparer.OrdinalIgnoreCase);
            var files = sizeByPath.Keys.ToList();

            var scannedNormalized = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);
            var processed = 0;
            var skipped = 0;
            var imported = 0;
            var failed = 0;

            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var size = sizeByPath.GetValueOrDefault(file);
                    if (incremental
                        && normalizedIndex.TryGetValue(file, out var oldSize)
                        && oldSize > 0
                        && size > 0
                        && oldSize == size)
                    {
                        skipped++;
                        processed++;
                        ProgressChanged?.Invoke(this, new ScanProgressEventArgs(processed, files.Count, file, skipped));
                        continue;
                    }

                    var track = await WebDavTrackMetadataReader.ReadTrackAsync(
                        _webDav, endpoint!, file, size, ct).ConfigureAwait(false);
                    track.LibraryId = libraryId;
                    TrackMetadataHelper.EnsureDuration(track);
                    await _tracks.UpsertAsync(track, ct, validateArtistNames: false);
                    imported++;
                }
                catch
                {
                    failed++;
                }

                processed++;
                ProgressChanged?.Invoke(this, new ScanProgressEventArgs(processed, files.Count, file, skipped));
            }

            var toRemove = new List<string>();
            foreach (var (dbPath, _) in rawIndex)
            {
                var norm = TrackPathNormalizer.Normalize(dbPath);
                if (scannedNormalized.Contains(norm)) continue;
                toRemove.Add(dbPath);
            }

            if (toRemove.Count > 0)
                await _tracks.RemoveByPathsAsync(toRemove, ct);

            var claimed = await _tracks.ClaimTracksUnderRootForScanAsync(libraryId, root, libs, ct);
            var dupes = await _tracks.RemoveDuplicateNormalizedPathsAsync(libraryId, ct);
            await EnrichWebDavMetadataAsync(libraryId, root, endpoint, ct).ConfigureAwait(false);
            var dbCount = await _tracks.CountByLibraryScopeAsync(libraryId, root, ct);

            return new LibraryScanResult
            {
                RootPath = root,
                DiskFileCount = files.Count,
                ImportedCount = imported,
                FailedCount = failed,
                DbTrackCount = dbCount,
                ClaimedCount = claimed,
                DuplicatesRemoved = dupes
            };
        }
        finally { IsScanning = false; }
    }

    private async Task EnrichWebDavMetadataAsync(
        long libraryId,
        string root,
        NetworkLibraryEndpoint endpoint,
        CancellationToken ct)
    {
        var tracks = await _tracks.GetAllUnderLibraryScopeAsync(libraryId, root, ct).ConfigureAwait(false);
        foreach (var existing in tracks)
        {
            if (!WebDavPathHelper.IsWebDavPath(existing.FilePath))
                continue;

            var needsDuration = existing.DurationMs <= 500;
            var needsMeta = string.IsNullOrWhiteSpace(existing.Album)
                            || string.IsNullOrWhiteSpace(existing.Artist);
            if (!needsDuration && !needsMeta)
                continue;

            try
            {
                var fresh = await WebDavTrackMetadataReader.ReadTrackAsync(
                    _webDav,
                    endpoint,
                    existing.FilePath,
                    existing.FileSizeBytes,
                    ct).ConfigureAwait(false);
                fresh.LibraryId = libraryId;
                TrackMetadataHelper.MergePreferNonEmpty(fresh, existing);
                TrackMetadataHelper.EnsureDuration(fresh);
                await _tracks.UpsertAsync(fresh, ct, validateArtistNames: false).ConfigureAwait(false);
            }
            catch
            {
                // 单文件失败不影响整库
            }
        }
    }

    private async Task EnrichMetadataFromDiskAsync(long libraryId, string root, CancellationToken ct)
    {
        var tracks = await _tracks.GetAllUnderLibraryScopeAsync(libraryId, root, ct);
        foreach (var existing in tracks)
        {
            if (WebDavPathHelper.IsWebDavPath(existing.FilePath))
                continue;

            var needsDuration = existing.DurationMs <= 500;
            var needsMeta = string.IsNullOrWhiteSpace(existing.Album)
                              || string.IsNullOrWhiteSpace(existing.Artist);
            if (!needsDuration && !needsMeta)
                continue;

            try
            {
                if (!File.Exists(existing.FilePath))
                    continue;

                var fresh = await TrackMetadataHelper.ReadFromFileAsync(existing.FilePath, ct);
                fresh.LibraryId = libraryId;
                fresh.FilePath = TrackPathNormalizer.Normalize(existing.FilePath);
                TrackMetadataHelper.MergePreferNonEmpty(fresh, existing);
                TrackMetadataHelper.EnsureDuration(fresh);
                await _tracks.UpsertAsync(fresh, ct, validateArtistNames: false);
            }
            catch
            {
                // 单文件失败不影响整库
            }
        }
    }

    /// <summary>扫描所有已启用音乐库</summary>
    public async Task<IReadOnlyList<LibraryScanResult>> ScanAllEnabledAsync(bool incremental = true, CancellationToken ct = default)
    {
        var results = await AppMetrics.MeasureAsync("scan.all", async () =>
        {
            var list = new List<LibraryScanResult>();
            var libs = await _libraries.GetAllAsync(ct);
            foreach (var lib in libs.Where(l => l.IsEnabled))
            {
                var result = await ScanLibraryAsync(lib.Id, incremental, ct);
                list.Add(result);
                AppMetrics.TracksScanned.Add(result.DiskFileCount);
                AppMetrics.TracksImported.Add(result.ImportedCount);
            }
            return (IReadOnlyList<LibraryScanResult>)list;
        });

        var totalTracks = results.Sum(r => r.DbTrackCount);
        AppMetrics.SetLibraryTrackCount(totalTracks);

        return results;
    }

    /// <summary>后台自动扫描（不阻塞 UI）</summary>
    public void StartBackgroundScan(bool incremental = true)
    {
        _backgroundCts?.Cancel();
        _backgroundCts = new CancellationTokenSource();
        var token = _backgroundCts.Token;
        _ = Task.Run(async () =>
        {
            try { await ScanAllEnabledAsync(incremental, token); }
            catch (OperationCanceledException) { }
        }, token);
    }

    public void StopBackgroundScan() => _backgroundCts?.Cancel();
}

/// <summary>扫描进度事件参数</summary>
public sealed class ScanProgressEventArgs(
    int current,
    int total,
    string currentFile,
    int skipped = 0,
    int acoustIdLookupAttempts = 0,
    int acoustIdLookupMatches = 0) : EventArgs
{
    public int Current { get; } = current;
    public int Total { get; } = total;
    public string CurrentFile { get; } = currentFile;
    public int Skipped { get; } = skipped;
    public int AcoustIdLookupAttempts { get; } = acoustIdLookupAttempts;
    public int AcoustIdLookupMatches { get; } = acoustIdLookupMatches;
    public double Percent => Total > 0 ? (double)Current / Total * 100 : 0;
}
