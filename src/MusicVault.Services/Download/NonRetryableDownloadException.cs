namespace MusicVault.Services.Download;

/// <summary>不应自动重试的下载错误（如假 FLAC、文件无效）。</summary>
public sealed class NonRetryableDownloadException : Exception
{
    public NonRetryableDownloadException(string message) : base(message) { }
}
