namespace MusicVault.Core.Models;

using MusicVault.Core.Enums;

/// <summary>独立音乐库配置</summary>
public sealed class MusicLibrary
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RootPath { get; set; } = string.Empty;
    public LibrarySourceType SourceType { get; set; } = LibrarySourceType.Local;
    public string? RemoteEndpointJson { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string FileNameTemplate { get; set; } = "{Title} - {Artist}.{Extension}";
    public string FolderTemplate { get; set; } = "{Artist}/{Album}";
    public string[] ScanExtensions { get; set; } =
        [".mp3", ".flac", ".m4a", ".aac", ".ogg", ".wav", ".wma", ".ape"];
    public DateTime? LastScanAt { get; set; }
}
