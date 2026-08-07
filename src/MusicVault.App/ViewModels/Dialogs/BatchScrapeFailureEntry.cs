namespace MusicVault.App.ViewModels.Dialogs;

/// <summary>批量刮削失败明细行。</summary>
public sealed record BatchScrapeFailureEntry(string TrackLabel, string Reason, string? FileName = null);
