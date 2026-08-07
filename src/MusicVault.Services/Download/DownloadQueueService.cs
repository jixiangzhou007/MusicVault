using System.Collections.Concurrent;
using MusicVault.Core.Audio;
using MusicVault.Core.Diagnostics;
using MusicVault.Core.Enums;
using MusicVault.Core.Interfaces;
using MusicVault.Core.Models;
using MusicVault.Data.Repositories;
using MusicVault.Services.Api;
using MusicVault.Services.Deduplication;
using MusicVault.Services.Library;
using MusicVault.Services.Metadata;
using MusicVault.Core.Nas;
using MusicVault.Services.Organize;
using MusicVault.Services.Providers;
using MusicVault.Services.Search;

namespace MusicVault.Services.Download;

/// <summary>
/// 下载队列：断点续传、自动重试3次、暂停/继续/取消、去重、完成后索引。
/// </summary>
public sealed class DownloadQueueService
{
    private readonly ConcurrentDictionary<Guid, DownloadItem> _queue = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _cts = new();
    private readonly IEnumerable<IMusicSourceProvider> _providers;
    private readonly IMetadataService _metadata;
    private readonly TrackRepository _tracks;
    private readonly LibraryRepository _libraries;
    private readonly LibraryManagerService _libraryManager;
    private readonly DuplicateDetectionService _dedup;
    private readonly DownloadFailureRepository? _failures;
    private readonly DownloadHistoryRepository _history;
    private readonly WebDavClient? _webDav;
    private readonly WebDavLibraryService? _webDavLibraries;
    private string _downloadRoot;
    private readonly SemaphoreSlim _semaphore = new(3);
    private readonly ConcurrentDictionary<Guid, DateTime> _lastProgressReportUtc = new();
    private const int MaxRetries = 3;
    private const int ProgressReportIntervalMs = 150;

    public event EventHandler? QueueChanged;
    public event EventHandler<DownloadProgressEventArgs>? ProgressChanged;
    public event EventHandler<DownloadItem>? DownloadCompleted;
    public event EventHandler<DownloadItem>? DownloadFailed;

    public DownloadQueueService(
        IEnumerable<IMusicSourceProvider> providers,
        IMetadataService metadata,
        TrackRepository tracks,
        LibraryRepository libraries,
        LibraryManagerService libraryManager,
        string downloadRoot,
        DownloadFailureRepository? failures = null,
        DownloadHistoryRepository? history = null,
        WebDavClient? webDav = null,
        WebDavLibraryService? webDavLibraries = null)
    {
        _providers = providers;
        _metadata = metadata;
        _tracks = tracks;
        _libraries = libraries;
        _libraryManager = libraryManager;
        _dedup = new DuplicateDetectionService(tracks);
        _failures = failures;
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _webDav = webDav;
        _webDavLibraries = webDavLibraries;
        _downloadRoot = downloadRoot;
        Directory.CreateDirectory(_downloadRoot);
    }

    public string DownloadRoot => _downloadRoot;

    public async Task LoadHistoryAsync(CancellationToken ct = default)
    {
        foreach (var item in await _history.GetAllAsync(ct))
        {
            if (string.IsNullOrWhiteSpace(item.DisplayTitle))
                item.DisplayTitle = item.Track.Title;
            if (string.IsNullOrWhiteSpace(item.DisplayArtist))
                item.DisplayArtist = item.Track.Artist;

            if (item.Status is DownloadStatus.Pending or DownloadStatus.Downloading or DownloadStatus.Paused)
            {
                if (DownloadOutcomeHelper.HasValidAudioFile(item))
                    DownloadOutcomeHelper.ReconcileStatus(item);
                else
                {
                    item.Status = DownloadStatus.Failed;
                    item.ErrorMessage ??= ServiceMessageCodec.Key("Download.IncompleteRetry");
                    item.FinishedAtUtc ??= DateTime.UtcNow;
                }
            }
            else
            {
                DownloadOutcomeHelper.ReconcileStatus(item);
            }

            _queue[item.Id] = item;
        }

        ReconcileAllStatuses(persist: true);
    }

    /// <summary>根据磁盘文件修正误判的失败/中断状态，并可选写回数据库。</summary>
    public void ReconcileAllStatuses(bool persist = false)
    {
        var changed = false;
        foreach (var item in _queue.Values)
        {
            var before = item.Status;
            DownloadOutcomeHelper.ReconcileStatus(item);
            if (before != item.Status)
                changed = true;
        }

        if (changed || persist)
            NotifyQueueChanged(persist: persist);
    }

    public Task FlushHistoryAsync(CancellationToken ct = default) =>
        _history.SaveNowAsync(_queue.Values, ct);

    public void SetDownloadRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException(ServiceMessageCodec.Key("Download.DirectoryEmpty"), nameof(path));
        _downloadRoot = path;
        Directory.CreateDirectory(_downloadRoot);
    }

    public IReadOnlyList<DownloadItem> Items =>
        _queue.Values
            .OrderByDescending(x => x.FinishedAtUtc ?? x.CreatedAtUtc)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToList();

    /// <summary>完整下载台账：数据库记录与内存队列合并（队列优先反映实时进度）。</summary>
    public async Task<IReadOnlyList<DownloadItem>> GetAllHistoryItemsAsync(CancellationToken ct = default)
    {
        var merged = new Dictionary<Guid, DownloadItem>();
        foreach (var item in await _history.GetAllAsync(ct))
            merged[item.Id] = item;
        foreach (var live in _queue.Values)
            merged[live.Id] = live;

        return merged.Values
            .OrderByDescending(x => x.FinishedAtUtc ?? x.CreatedAtUtc)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToList();
    }

    public IReadOnlyList<DownloadItem> FailedItems =>
        Items.Where(i => i.Status == DownloadStatus.Failed).ToList();

    /// <summary>添加下载任务（含去重检查）</summary>
    public bool Enqueue(
        OnlineTrack track,
        AudioQuality quality,
        long? libraryId = null,
        string? targetDirectory = null)
    {
        var ext = quality == AudioQuality.Flac ? ".flac" : ".mp3";
        var safeName = $"{track.Artist} - {track.Title}{ext}".ReplaceInvalidChars();
        var root = string.IsNullOrWhiteSpace(targetDirectory)
            ? _downloadRoot
            : targetDirectory.Trim();
        Directory.CreateDirectory(root);
        var targetPath = Path.Combine(root, safeName);

        if (File.Exists(targetPath))
        {
            var fi = new FileInfo(targetPath);
            var onDisk = new Track { FilePath = targetPath, Title = track.Title, Artist = track.Artist, FileSizeBytes = fi.Length };
            var incoming = new Track { Title = track.Title, Artist = track.Artist, FileSizeBytes = fi.Length };
            if (_dedup.IsLikelyDuplicate(onDisk, incoming))
                return false;
        }

        var item = new DownloadItem
        {
            Track = track,
            DisplayTitle = track.Title.Trim(),
            DisplayArtist = track.Artist.Trim(),
            Quality = quality,
            LibraryId = libraryId,
            TargetPath = targetPath,
            Status = DownloadStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            EstimatedTotalBytes = OnlineTrackDisplaySize.TryResolveFromSearchMetadata(track, quality) ?? 0
        };
        _queue[item.Id] = item;
        NotifyQueueChanged();
        _ = ProcessItemAsync(item);
        return true;
    }

    /// <summary>将 NAS 曲目复制到指定本地音乐库（纳入传输队列）。</summary>
    public bool EnqueueNasCopyToLibrary(
        Track sourceTrack,
        MusicLibrary targetLibrary,
        FileOrganizerService organizer)
    {
        if (!NasPathHelper.IsRemoteLibraryPath(sourceTrack.FilePath))
            return false;

        if (NasPathHelper.IsUncPath(sourceTrack.FilePath) && !File.Exists(sourceTrack.FilePath))
            throw new FileNotFoundException(ServiceMessageCodec.Key("Download.NasSourceMissing"), sourceTrack.FilePath);

        var preview = organizer.PreviewOrganize(
            sourceTrack,
            targetLibrary.RootPath,
            targetLibrary.FolderTemplate,
            targetLibrary.FileNameTemplate);
        var targetPath = EnsureUniqueTargetPath(preview.TargetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        if (File.Exists(targetPath))
        {
            var fi = new FileInfo(targetPath);
            var onDisk = new Track
            {
                FilePath = targetPath,
                Title = sourceTrack.Title,
                Artist = sourceTrack.Artist,
                FileSizeBytes = fi.Length
            };
            var incoming = new Track { Title = sourceTrack.Title, Artist = sourceTrack.Artist, FileSizeBytes = fi.Length };
            if (_dedup.IsLikelyDuplicate(onDisk, incoming))
                return false;
        }

        long totalBytes;
        try
        {
            totalBytes = sourceTrack.FileSizeBytes > 0
                ? sourceTrack.FileSizeBytes
                : NasPathHelper.IsUncPath(sourceTrack.FilePath)
                    ? new FileInfo(sourceTrack.FilePath).Length
                    : 0;
        }
        catch
        {
            totalBytes = 0;
        }

        var quality = NasLibraryDownloadService.InferQuality(sourceTrack.FilePath);
        var item = new DownloadItem
        {
            SourceKind = DownloadSourceKind.NasLocalLibrary,
            SourceTrackId = sourceTrack.Id,
            SourceFilePath = sourceTrack.FilePath,
            Track = new OnlineTrack
            {
                Platform = MusicPlatform.NasLibrary,
                Id = sourceTrack.Id.ToString(),
                Title = sourceTrack.Title,
                Artist = sourceTrack.Artist,
                Album = sourceTrack.Album,
                DurationMs = sourceTrack.DurationMs,
                HasFlac = quality == AudioQuality.Flac
            },
            DisplayTitle = sourceTrack.Title.Trim(),
            DisplayArtist = sourceTrack.Artist.Trim(),
            Quality = quality,
            LibraryId = targetLibrary.Id,
            TargetPath = targetPath,
            Status = DownloadStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            TotalBytes = totalBytes,
            EstimatedTotalBytes = totalBytes
        };

        _queue[item.Id] = item;
        NotifyQueueChanged();
        _ = ProcessItemAsync(item);
        return true;
    }

    private static string EnsureUniqueTargetPath(string targetPath)
    {
        if (!File.Exists(targetPath))
            return targetPath;

        var dir = Path.GetDirectoryName(targetPath) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(targetPath);
        var ext = Path.GetExtension(targetPath);
        for (var i = 2; i < 1000; i++)
        {
            var candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(candidate))
                return candidate;
        }

        return Path.Combine(dir, $"{name}_{Guid.NewGuid():N}{ext}");
    }

    public void Pause(Guid id)
    {
        if (!_queue.TryGetValue(id, out var item)) return;
        item.Status = DownloadStatus.Paused;
        if (_cts.TryRemove(id, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
        NotifyQueueChanged();
    }

    public void Resume(Guid id)
    {
        if (_queue.TryGetValue(id, out var item) && item.Status == DownloadStatus.Paused)
        {
            item.Status = DownloadStatus.Pending;
            item.FinishedAtUtc = null;
            NotifyQueueChanged();
            _ = ProcessItemAsync(item);
        }
    }

    public void Cancel(Guid id)
    {
        if (_cts.TryRemove(id, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
        if (_queue.TryGetValue(id, out var item))
        {
            item.Status = DownloadStatus.Cancelled;
            item.FinishedAtUtc = DateTime.UtcNow;
            NotifyQueueChanged();
        }
    }

    public void ClearCompleted()
    {
        foreach (var key in _queue.Where(x => x.Value.Status is DownloadStatus.Completed or DownloadStatus.Cancelled)
                     .Select(x => x.Key).ToList())
            _queue.TryRemove(key, out _);
        NotifyQueueChanged(persist: false);
    }

    /// <summary>清空活跃队列（发现页速览）；台账记录保留在数据库。</summary>
    public void ClearQueue()
    {
        foreach (var id in _cts.Keys.ToList())
        {
            if (_cts.TryRemove(id, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
        }

        _queue.Clear();
        NotifyQueueChanged(persist: false);
    }

    /// <summary>清空全部下载台账（含进行中任务，会先取消）。</summary>
    public async Task ClearAllHistoryAsync(CancellationToken ct = default)
    {
        foreach (var id in _cts.Keys.ToList())
        {
            if (_cts.TryRemove(id, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
        }

        _queue.Clear();
        await _history.ClearAllAsync(ct);
        QueueChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>清空全部下载记录（含进行中任务，会先取消）。</summary>
    public void ClearAll() => _ = ClearAllHistoryAsync();

    /// <summary>从活跃队列移除（不删台账）。</summary>
    public void RemoveFromQueue(Guid id)
    {
        if (_queue.TryGetValue(id, out var item)
            && item.Status is DownloadStatus.Downloading or DownloadStatus.Pending or DownloadStatus.Paused)
            Cancel(id);
        _queue.TryRemove(id, out _);
        NotifyQueueChanged(persist: false);
    }

    /// <summary>从列表与台账移除（进行中任务会先取消）。</summary>
    public void RemoveRecord(Guid id)
    {
        if (_queue.TryGetValue(id, out var item)
            && item.Status is DownloadStatus.Downloading or DownloadStatus.Pending or DownloadStatus.Paused)
            Cancel(id);
        _queue.TryRemove(id, out _);
        _ = _history.DeleteAsync(id);
        NotifyQueueChanged(persist: false);
    }

    /// <summary>从列表移除（进行中任务会先取消）。</summary>
    public void Remove(Guid id) => RemoveRecord(id);

    /// <summary>取消所有进行中的下载（应用退出时调用）</summary>
    public void CancelAll()
    {
        foreach (var id in _cts.Keys.ToList())
            Cancel(id);
    }

    /// <summary>重试所有失败任务</summary>
    public void RetryAllFailed()
    {
        foreach (var item in _queue.Values.Where(i => i.Status == DownloadStatus.Failed))
        {
            RetryItem(item);
            _ = ProcessItemAsync(item);
        }
        NotifyQueueChanged();
    }

    public void Retry(Guid id)
    {
        if (!_queue.TryGetValue(id, out var item) || item.Status != DownloadStatus.Failed) return;
        RetryItem(item);
        NotifyQueueChanged();
        _ = ProcessItemAsync(item);
    }

    private static void RetryItem(DownloadItem item)
    {
        item.Status = DownloadStatus.Pending;
        item.RetryCount = 0;
        item.BytesDownloadComplete = false;
        item.ErrorMessage = null;
        item.FinishedAtUtc = null;
    }

    private void NotifyQueueChanged(bool persist = true)
    {
        QueueChanged?.Invoke(this, EventArgs.Empty);
        if (persist)
            _history.ScheduleUpsertBatch(_queue.Values);
    }

    private void MarkFinished(DownloadItem item, DownloadStatus status)
    {
        item.Status = status;
        item.FinishedAtUtc = DateTime.UtcNow;
        _lastProgressReportUtc.TryRemove(item.Id, out _);
        ProgressChanged?.Invoke(this, new DownloadProgressEventArgs(item.Id, item.Progress));

        if (status == DownloadStatus.Completed)
        {
            AppMetrics.DownloadsCompleted.Add(1);
            AppMetrics.DownloadDuration.Record((DateTime.UtcNow - item.CreatedAtUtc).TotalMilliseconds);
        }
        else if (status == DownloadStatus.Failed)
        {
            AppMetrics.DownloadsFailed.Add(1);
        }

        if (status == DownloadStatus.Failed)
            DownloadFailed?.Invoke(this, item);
    }

    private async Task ProcessItemAsync(DownloadItem item)
    {
        if (item.Status is DownloadStatus.Downloading or DownloadStatus.Completed) return;

        await _semaphore.WaitAsync();
        var cts = new CancellationTokenSource();
        _cts[item.Id] = cts;

        try
        {
            if (item.Status == DownloadStatus.Paused) return;

            item.Status = DownloadStatus.Downloading;
            item.BytesDownloadComplete = false;
            item.FinishedAtUtc = null;
            _lastProgressReportUtc.TryRemove(item.Id, out _);
            NotifyQueueChanged();

            if (item.SourceKind == DownloadSourceKind.NasLocalLibrary)
            {
                await ProcessNasCopyAsync(item, cts.Token).ConfigureAwait(false);
                return;
            }

            if (GoMusicApiClient.IsConfigured)
                await MusicApiSessionHelper.EnsureReadyAsync(cts.Token).ConfigureAwait(false);

            var provider = ResolveProvider(item)
                ?? throw new InvalidOperationException(ServiceMessageCodec.Format("Download.PlatformNotFound", item.Track.Platform));

            while (item.RetryCount < MaxRetries)
            {
                if (item.Status == DownloadStatus.Paused) return;

                string? lastUrl = null;
                try
                {
                    lastUrl = await ResolveDownloadUrlAsync(item, provider, cts.Token).ConfigureAwait(false);
                    provider = ResolveProvider(item) ?? provider;
                    await DownloadWithProgressAsync(lastUrl, item, cts.Token);
                    await FinalizeSuccessfulDownloadAsync(item, cts.Token).ConfigureAwait(false);
                    break;
                }
                catch (OperationCanceledException)
                {
                    if (item.Status == DownloadStatus.Paused) return;
                    throw;
                }
                catch (FlacFormatMismatchException ex)
                {
                    ResetDownloadTransferState(item);

                    if (GoMusicApiClient.IsConfigured
                        && await TrySwitchTrackSourceAsync(item, cts.Token).ConfigureAwait(false))
                    {
                        item.ErrorMessage = ServiceMessageCodec.Key("Download.FlacValidationRetry");
                        NotifyQueueChanged();
                        continue;
                    }

                    item.ErrorMessage = DownloadFailureMessageMapper.MapException(ex);
                    MarkFinished(item, DownloadStatus.Failed);
                    if (_failures != null)
                        await _failures.RecordAsync(item.Track.Platform, item.Track.Id,
                            item.DisplayTitle, item.DisplayArtist, item.Quality, item.ErrorMessage, cts.Token);
                    break;
                }
                catch (NonRetryableDownloadException ex)
                {
                    item.ErrorMessage = DownloadFailureMessageMapper.MapException(ex);
                    MarkFinished(item, DownloadStatus.Failed);
                    if (_failures != null)
                        await _failures.RecordAsync(item.Track.Platform, item.Track.Id,
                            item.DisplayTitle, item.DisplayArtist, item.Quality, item.ErrorMessage, cts.Token);
                    break;
                }
                catch (Exception ex)
                {
                    ResetDownloadTransferState(item);

                    if (GoMusicApiClient.IsConfigured
                        && !GoMusicApiClient.IsStreamProxyUrl(lastUrl)
                        && GoMusicApiClient.TryBuildStreamDownloadUrl(item.Track, item.Quality) is { } streamUrl)
                    {
                        try
                        {
                            item.ErrorMessage = ServiceMessageCodec.Key("Download.DirectLinkProxyRetry");
                            NotifyQueueChanged();
                            await DownloadWithProgressAsync(streamUrl, item, cts.Token);
                            await FinalizeSuccessfulDownloadAsync(item, cts.Token).ConfigureAwait(false);
                            break;
                        }
                        catch (NonRetryableDownloadException streamFail)
                        {
                            item.ErrorMessage = DownloadFailureMessageMapper.MapException(streamFail);
                            MarkFinished(item, DownloadStatus.Failed);
                            if (_failures != null)
                                await _failures.RecordAsync(item.Track.Platform, item.Track.Id,
                                    item.DisplayTitle, item.DisplayArtist, item.Quality, item.ErrorMessage, cts.Token);
                            break;
                        }
                        catch
                        {
                            ResetDownloadTransferState(item);
                        }
                    }

                    if (item.RetryCount < MaxRetries
                        && GoMusicApiClient.IsConfigured
                        && await TrySwitchTrackSourceAsync(item, cts.Token).ConfigureAwait(false))
                    {
                        item.ErrorMessage = ServiceMessageCodec.Key("Download.SourceSwitchRetry");
                        NotifyQueueChanged();
                        continue;
                    }

                    item.RetryCount++;
                    item.ErrorMessage = DownloadFailureMessageMapper.MapException(ex);
                    if (item.RetryCount >= MaxRetries)
                    {
                        MarkFinished(item, DownloadStatus.Failed);
                        if (_failures != null)
                            await _failures.RecordAsync(item.Track.Platform, item.Track.Id,
                                item.DisplayTitle, item.DisplayArtist, item.Quality, item.ErrorMessage, cts.Token);
                    }
                    else
                        await Task.Delay(2000 * item.RetryCount, cts.Token);
                }
            }
        }
        finally
        {
            if (_cts.TryRemove(item.Id, out var removed))
                removed.Dispose();
            _semaphore.Release();
            NotifyQueueChanged();
        }
    }

    private static async Task<string> ResolveDownloadUrlAsync(
        DownloadItem item,
        IMusicSourceProvider provider,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(item.Track.DirectDownloadUrl))
            return item.Track.DirectDownloadUrl;

        if (item.Track.Platform == MusicPlatform.MyFreeMp3
            && provider is MyFreeMp3MusicProvider myFreeMp3)
        {
            var myFreeUrl = await myFreeMp3.ResolveDownloadForTrackAsync(item.Track, item.Quality, ct)
                .ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(myFreeUrl))
                return myFreeUrl;
        }
        else if (!string.IsNullOrWhiteSpace(item.Track.QuarkShareUrl) && MyFreeMp3QuarkConfig.IsConfigured)
        {
            var quarkUrl = await QuarkShareParser.ResolveDownloadUrlAsync(
                    item.Track.QuarkShareUrl, MyFreeMp3QuarkConfig.Cookies, ct)
                .ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(quarkUrl))
                return quarkUrl;
        }

        var rotated = await GoMusicApiClient.TryResolveDownloadWithSourceRotationAsync(
                item.Track, item.Quality, ct)
            .ConfigureAwait(false);
        if (rotated != null)
        {
            ApplySwitchedTrack(item, rotated.Value.Track);
            return rotated.Value.Url;
        }

        return await provider.ResolveDownloadUrlAsync(item.Track.Id, item.Quality, ct).ConfigureAwait(false);
    }

    private async Task ProcessNasCopyAsync(DownloadItem item, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(item.SourceFilePath) || string.IsNullOrWhiteSpace(item.TargetPath))
        {
            item.ErrorMessage = ServiceMessageCodec.Key("Download.NasCopyMissingPaths");
            MarkFinished(item, DownloadStatus.Failed);
            return;
        }

        while (item.RetryCount < MaxRetries)
        {
            if (item.Status == DownloadStatus.Paused)
                return;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(item.TargetPath)!);
                if (WebDavPathHelper.IsWebDavPath(item.SourceFilePath))
                {
                    if (_webDav == null || _webDavLibraries == null || item.SourceTrackId is not > 0)
                        throw new InvalidOperationException(ServiceMessageCodec.Key("Download.WebDavCopyNotConfigured"));

                    var track = await _tracks.GetByIdAsync(item.SourceTrackId.Value, ct).ConfigureAwait(false);
                    if (track?.LibraryId is not > 0)
                        throw new InvalidOperationException(ServiceMessageCodec.Key("Download.WebDavLibraryUnresolved"));

                    var endpoint = await _webDavLibraries.ResolveStoredEndpointAsync(track.LibraryId.Value, ct)
                        .ConfigureAwait(false);
                    if (endpoint == null)
                        throw new InvalidOperationException(ServiceMessageCodec.Key("Download.WebDavEndpointUnreachable"));

                    await _webDav.CopyToFileAsync(endpoint, item.SourceFilePath, item.TargetPath, ct)
                        .ConfigureAwait(false);
                }
                else
                {
                    if (!File.Exists(item.SourceFilePath))
                        throw new FileNotFoundException(ServiceMessageCodec.Key("Download.NasSourceMissing"), item.SourceFilePath);

                    await CopyFileWithProgressAsync(item.SourceFilePath, item.TargetPath, item, ct)
                        .ConfigureAwait(false);
                }

                await FinalizeSuccessfulDownloadAsync(item, ct).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException)
            {
                if (item.Status == DownloadStatus.Paused)
                    return;
                throw;
            }
            catch (Exception ex)
            {
                ResetDownloadTransferState(item);
                item.RetryCount++;
                item.ErrorMessage = DownloadFailureMessageMapper.MapException(ex);
                if (item.RetryCount >= MaxRetries)
                {
                    MarkFinished(item, DownloadStatus.Failed);
                    return;
                }

                await Task.Delay(2000 * item.RetryCount, ct).ConfigureAwait(false);
            }
        }
    }

    private async Task CopyFileWithProgressAsync(
        string sourcePath,
        string targetPath,
        DownloadItem item,
        CancellationToken ct)
    {
        var tempPath = targetPath + ".part";
        if (File.Exists(tempPath))
        {
            try { File.Delete(tempPath); } catch { /* ignore */ }
        }

        await using (var src = File.OpenRead(sourcePath))
        await using (var dst = File.Create(tempPath))
        {
            item.TotalBytes = item.TotalBytes > 0 ? item.TotalBytes : src.Length;
            item.EstimatedTotalBytes = item.TotalBytes;
            var buffer = new byte[81920];
            int read;
            while ((read = await src.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false)) > 0)
            {
                ct.ThrowIfCancellationRequested();
                if (item.Status == DownloadStatus.Paused)
                    return;

                await dst.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                item.DownloadedBytes += read;
                ReportProgressIfDue(item);
            }
        }

        if (File.Exists(targetPath))
            File.Delete(targetPath);
        File.Move(tempPath, targetPath);

        var finalLength = new FileInfo(targetPath).Length;
        item.DownloadedBytes = finalLength;
        if (item.TotalBytes <= 0 || item.TotalBytes < finalLength)
            item.TotalBytes = finalLength;
        item.BytesDownloadComplete = true;
        _lastProgressReportUtc.TryRemove(item.Id, out _);
        ProgressChanged?.Invoke(this, new DownloadProgressEventArgs(item.Id, item.Progress));
    }

    private async Task FinalizeSuccessfulDownloadAsync(DownloadItem item, CancellationToken ct)
    {
        if (item.Quality == AudioQuality.Flac)
        {
            var actual = AudioStreamFormatSniffer.ProbeFile(item.TargetPath).Format;
            if (actual != DetectedAudioFormat.Flac)
            {
                ResetDownloadTransferState(item);
                var detail = actual == DetectedAudioFormat.Mp3
                    ? "MP3"
                    : ServiceMessageCodec.Key("Download.FormatNotFlac");
                throw new FlacFormatMismatchException(
                    ServiceMessageCodec.Format("Download.FlacMismatchRejected", detail));
            }
        }

        if (!DownloadOutcomeHelper.HasValidAudioFile(item))
        {
            ResetDownloadTransferState(item);
            throw new NonRetryableDownloadException(ServiceMessageCodec.Key("Download.InvalidFile"));
        }

        item.ErrorMessage = await TryEnrichDownloadedFileAsync(item, ct).ConfigureAwait(false);

        MarkFinished(item, DownloadStatus.Completed);
        DownloadCompleted?.Invoke(this, item);
    }

    private IMusicSourceProvider? ResolveProvider(DownloadItem item) =>
        _providers.FirstOrDefault(p => p.Platform == item.Track.Platform);

    private static async Task<bool> TrySwitchTrackSourceAsync(DownloadItem item, CancellationToken ct)
    {
        var switched = await GoMusicApiClient.TrySwitchSourceAsync(item.Track, ct).ConfigureAwait(false);
        if (switched == null)
            return false;

        ApplySwitchedTrack(item, switched);
        item.DownloadedBytes = 0;
        item.TotalBytes = 0;
        item.BytesDownloadComplete = false;
        item.ErrorMessage = null;
        return true;
    }

    /// <summary>换源仅更新平台/ID 以下载；列表展示仍用用户点击时的歌名/歌手。</summary>
    private static void ApplySwitchedTrack(DownloadItem item, OnlineTrack switched)
    {
        var album = item.Track.Album;
        var directUrl = item.Track.DirectDownloadUrl;
        item.Track = switched;
        item.Track.Title = item.DisplayTitle;
        item.Track.Artist = item.DisplayArtist;
        item.Track.Album = album;
        if (!string.IsNullOrWhiteSpace(directUrl))
            item.Track.DirectDownloadUrl = directUrl;
    }

    /// <summary>写入标签、封面、歌词与曲库索引；失败不推翻「已下载」结论。</summary>
    private async Task<string?> TryEnrichDownloadedFileAsync(DownloadItem item, CancellationToken ct)
    {
        try
        {
            item.LibraryId ??= await ResolveLibraryIdForPathAsync(item.TargetPath, ct);

            var seed = new Track
            {
                LibraryId = item.LibraryId,
                FilePath = item.TargetPath,
                Title = item.DisplayTitle,
                Artist = item.DisplayArtist,
                Album = item.Track.Album ?? string.Empty,
                DurationMs = item.Track.DurationMs,
                FileSizeBytes = new FileInfo(item.TargetPath).Length
            };
            await _metadata.WriteTagsAsync(seed, ct);

            var localTrack = TrackMetadataHelper.ReadFromFile(item.TargetPath);
            localTrack.LibraryId = item.LibraryId;
            localTrack.FilePath = TrackPathNormalizer.Normalize(item.TargetPath);
            TrackMetadataHelper.MergePreferNonEmpty(localTrack, seed);
            TrackMetadataHelper.EnsureDuration(localTrack);

            var cover = await _metadata.DownloadCoverAsync(
                localTrack.Artist, localTrack.Album, 600, ct);
            if (cover != null)
                await _metadata.EmbedCoverAsync(item.TargetPath, cover, ct);

            var lyrics = await _metadata.DownloadLyricsAsync(
                localTrack.Artist, localTrack.Title, ct);
            if (lyrics != null)
            {
                await _metadata.EmbedLyricsAsync(item.TargetPath, lyrics, ct);
                localTrack.LyricsPath = Path.ChangeExtension(item.TargetPath, ".lrc");
            }

            localTrack.Id = await _tracks.UpsertAsync(localTrack, ct);
            if (item.LibraryId.HasValue)
            {
                await _tracks.SetLibraryIdByFilePathAsync(item.TargetPath, item.LibraryId.Value, ct);
                var lib = await _libraries.GetByIdAsync(item.LibraryId.Value, ct);
                if (lib != null)
                    await _tracks.AssignOrphansUnderRootAsync(lib.Id, lib.RootPath, ct);
            }

            return null;
        }
        catch (Exception ex)
        {
            return ServiceMessageCodec.Format("Download.EnrichIncomplete", ex.Message);
        }
    }

    private async Task DownloadWithProgressAsync(string url, DownloadItem item, CancellationToken ct)
    {
        var client = MusicVault.Services.Http.HttpClients.Download;
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (item.DownloadedBytes > 0)
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(item.DownloadedBytes, null);

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? 0;
        if (item.DownloadedBytes > 0 && total > 0) total += item.DownloadedBytes;
        if (total <= 0)
            total = item.EstimatedTotalBytes;
        item.TotalBytes = total;

        var mode = item.DownloadedBytes > 0 ? FileMode.Append : FileMode.Create;
        await using var fs = new FileStream(item.TargetPath, mode, FileAccess.Write);
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer, ct)) > 0)
        {
            ct.ThrowIfCancellationRequested();
            if (item.Status == DownloadStatus.Paused) return;
            await fs.WriteAsync(buffer.AsMemory(0, read), ct);
            item.DownloadedBytes += read;
            ReportProgressIfDue(item);
        }

        var finalLength = new FileInfo(item.TargetPath).Length;
        item.DownloadedBytes = finalLength;
        if (item.TotalBytes <= 0 || item.TotalBytes < finalLength)
            item.TotalBytes = finalLength;
        item.BytesDownloadComplete = true;
        _lastProgressReportUtc.TryRemove(item.Id, out _);
        ProgressChanged?.Invoke(this, new DownloadProgressEventArgs(item.Id, item.Progress));
    }

    private void ReportProgressIfDue(DownloadItem item)
    {
        var now = DateTime.UtcNow;
        if (_lastProgressReportUtc.TryGetValue(item.Id, out var last)
            && (now - last).TotalMilliseconds < ProgressReportIntervalMs)
            return;

        _lastProgressReportUtc[item.Id] = now;
        ProgressChanged?.Invoke(this, new DownloadProgressEventArgs(item.Id, item.Progress));
    }

    private async Task<long?> ResolveLibraryIdForPathAsync(string filePath, CancellationToken ct = default)
    {
        var libs = await _libraries.GetAllAsync(ct);
        var fromPath = LibraryPathMatcher.ResolveLibraryId(filePath, libs);
        if (fromPath.HasValue) return fromPath;

        var downloadRoot = LibraryRepository.NormalizeRootPath(_downloadRoot);
        try
        {
            var fullPath = Path.GetFullPath(filePath);
            var rel = Path.GetRelativePath(downloadRoot, fullPath);
            if (!rel.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(rel))
            {
                var (lib, _) = await _libraryManager.AddOrGetLibraryAsync(
                    Path.GetFileName(downloadRoot) is { Length: > 0 } n
                        ? n
                        : ServiceMessages.Get("Library.DefaultDownloadFolderName", ServiceCulture.Current),
                    downloadRoot, ct);
                return lib.Id;
            }
        }
        catch
        {
            // ignore path errors
        }

        return null;
    }

    private void ResetDownloadTransferState(DownloadItem item)
    {
        TryDeletePartialFile(item.TargetPath);
        item.DownloadedBytes = 0;
        item.TotalBytes = 0;
        item.BytesDownloadComplete = false;
        _lastProgressReportUtc.TryRemove(item.Id, out _);
        ProgressChanged?.Invoke(this, new DownloadProgressEventArgs(item.Id, 0));
    }

    private static void TryDeletePartialFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // ignore
        }
    }
}

/// <summary>下载进度事件</summary>
public sealed class DownloadProgressEventArgs(Guid itemId, double percent) : EventArgs
{
    public Guid ItemId { get; } = itemId;
    public double Percent { get; } = percent;
}

internal static class StringExtensions
{
    public static string ReplaceInvalidChars(this string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Trim();
    }
}
