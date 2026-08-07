using MusicVault.App.Localization;
using MusicVault.App.ViewModels;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Core.Nas;
using MusicVault.Services;
using MusicVault.Services.Library;

namespace MusicVault.App.Services;

/// <summary>远程库（NAS / WebDAV）列表状态文案与离线容量统计。</summary>
public static class NasLibraryUiHelper
{
    public static async Task<long> ResolveLibrarySizeBytesAsync(
        AppLibraryFacade library,
        MusicLibrary lib,
        CancellationToken ct = default)
    {
        if (IsRemoteLibraryOffline(library, lib))
            return await library.Tracks.SumFileSizeBytesAsync(lib.Id, ct).ConfigureAwait(false);

        if (lib.SourceType == LibrarySourceType.NetworkWebDav)
            return await library.Tracks.SumFileSizeBytesAsync(lib.Id, ct).ConfigureAwait(false);

        return await Task.Run(() =>
            LibraryFolderSizeHelper.SumAudioFileBytes(lib.RootPath, lib.ScanExtensions), ct).ConfigureAwait(false);
    }

    public static async Task<long> SumEnabledLibrariesSizeAsync(
        AppLibraryFacade library,
        IEnumerable<MusicLibrary> libs,
        CancellationToken ct = default)
    {
        long sum = 0;
        foreach (var lib in libs)
            sum += await ResolveLibrarySizeBytesAsync(library, lib, ct).ConfigureAwait(false);
        return sum;
    }

    public static NasLibraryStatus? GetNetworkLibraryStatus(AppLibraryFacade library, MusicLibrary lib) =>
        lib.SourceType switch
        {
            LibrarySourceType.NetworkNas => library.NasLibraries.GetStatus(lib.Id),
            LibrarySourceType.NetworkWebDav => library.WebDavLibraries.GetStatus(lib.Id),
            _ => null
        };

    public static bool IsRemoteLibraryOffline(AppLibraryFacade library, MusicLibrary lib)
    {
        if (lib.SourceType == LibrarySourceType.NetworkNas)
        {
            var status = library.NasLibraries.GetStatus(lib.Id);
            if (status?.Reachability == NasReachability.Offline)
                return true;

            return !Directory.Exists(lib.RootPath);
        }

        if (lib.SourceType == LibrarySourceType.NetworkWebDav)
            return library.WebDavLibraries.GetStatus(lib.Id)?.Reachability == NasReachability.Offline;

        return false;
    }

    public static bool IsNasOffline(NasLibraryService nas, MusicLibrary lib) =>
        lib.SourceType == LibrarySourceType.NetworkNas
        && (nas.GetStatus(lib.Id)?.Reachability == NasReachability.Offline || !Directory.Exists(lib.RootPath));

    public static string? GetStatusSegment(NasReachability reachability, ILocalizationService l10n) =>
        reachability switch
        {
            NasReachability.Online => l10n.Get("Nas.Status.Online"),
            NasReachability.Offline => l10n.Get("Nas.Status.Offline"),
            _ => null
        };

    public static string? GetStatusToolTip(NasLibraryStatus? status, string? fallbackPath, ILocalizationService l10n)
    {
        if (status == null)
            return fallbackPath;

        var segment = GetStatusSegment(status.Reachability, l10n);
        if (string.IsNullOrWhiteSpace(status.Message))
            return segment ?? fallbackPath;

        return segment == null
            ? ServiceMessageLocalizer.Resolve(l10n, status.Message)
            : $"{segment}\n{ServiceMessageLocalizer.Resolve(l10n, status.Message)}";
    }

    public static void ApplyNetworkLibraryStatuses(
        IEnumerable<LibraryListItemViewModel> items,
        AppLibraryFacade library,
        ILocalizationService l10n)
    {
        foreach (var item in items.Where(i => i.IsNetworkSource && i.LibraryId.HasValue && i.Library != null))
        {
            var status = GetNetworkLibraryStatus(library, item.Library!);
            var segment = status == null ? null : GetStatusSegment(status.Reachability, l10n);
            var isWarning = status?.Reachability == NasReachability.Offline;
            item.UpdateNasStatus(segment, GetStatusToolTip(status, item.HoverPath, l10n), isWarning);
        }
    }

    public static void ApplyNasStatuses(
        IEnumerable<LibraryListItemViewModel> items,
        NasLibraryService nas,
        ILocalizationService l10n)
    {
        // 兼容旧调用：仅 SMB NAS 库
        foreach (var item in items.Where(i => i.Library?.SourceType == LibrarySourceType.NetworkNas && i.LibraryId.HasValue))
        {
            var status = nas.GetStatus(item.LibraryId!.Value);
            var segment = status == null ? null : GetStatusSegment(status.Reachability, l10n);
            var isWarning = status?.Reachability == NasReachability.Offline;
            item.UpdateNasStatus(segment, GetStatusToolTip(status, item.HoverPath, l10n), isWarning);
        }
    }

    public static string FormatScanSummary(LibraryScanResult result, ILocalizationService l10n)
    {
        if (result.RootMissing)
        {
            if (result.IsNetworkWebDav)
            {
                if (result.DbTrackCount > 0)
                    return string.Format(l10n.Get("WebDav.Scan.OfflineWithCache"), result.RootPath, result.DbTrackCount);
                return string.Format(l10n.Get("WebDav.Scan.Offline"), result.RootPath);
            }

            if (result.IsNetworkNas)
            {
                if (result.DbTrackCount > 0)
                    return string.Format(l10n.Get("Nas.Scan.OfflineWithCache"), result.RootPath, result.DbTrackCount);
                return string.Format(l10n.Get("Nas.Scan.Offline"), result.RootPath);
            }

            return string.Format(l10n.Get("Library.Tracks.ScanFolderMissing"), result.RootPath);
        }

        var summary = string.Format(
            l10n.Get("Library.Scan.Summary"),
            result.DiskFileCount,
            result.ImportedCount,
            result.FailedCount,
            result.DbTrackCount);
        if (result.AcoustIdLookupAttempts <= 0)
            return summary;

        return summary + string.Format(
            l10n.Get("Library.Scan.AcoustIdSuffix"),
            result.AcoustIdLookupMatches,
            result.AcoustIdLookupAttempts);
    }

    public static string FormatBatchDownloadMessage(NasLibraryDownloadBatchResult result, ILocalizationService l10n)
    {
        if (result.Enqueued <= 0 && result.Unreachable > 0)
            return string.Format(l10n.Get("Nas.Download.BatchUnreachable"), result.Unreachable);

        if (result.Enqueued <= 0)
            return l10n.Get("Nas.Download.BatchNothing");

        return string.Format(
            l10n.Get("Nas.Download.BatchEnqueued"),
            result.Enqueued,
            result.TargetLibrary.Name,
            result.SkippedDuplicates,
            result.Unreachable);
    }
}
