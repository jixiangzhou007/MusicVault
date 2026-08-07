namespace MusicVault.Services.Download;

/// <summary>请求 FLAC 但下载内容非 FLAC，可尝试换源重试。</summary>
public sealed class FlacFormatMismatchException : Exception
{
    public FlacFormatMismatchException(string message) : base(message) { }
}
