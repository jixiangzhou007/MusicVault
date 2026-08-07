using MusicVault.Services;

namespace MusicVault.Services.Library;

public sealed record SmbConnectResult(bool Success, string Message, int? Win32Error = null)
{
    public static SmbConnectResult Ok(string message) =>
        new(true, message);

    public static SmbConnectResult Fail(string message, int? win32Error = null) =>
        new(false, message, win32Error);
}

/// <summary>SMB 共享挂载（Windows WNetAddConnection2）。</summary>
public interface ISmbShareConnector
{
    SmbConnectResult TryConnect(string shareRootUnc, string? username, string? password);

    void TryDisconnect(string shareRootUnc);
}

/// <summary>非 Windows：跳过挂载，仅依赖系统已有连接。</summary>
public sealed class PassThroughSmbShareConnector : ISmbShareConnector
{
    public SmbConnectResult TryConnect(string shareRootUnc, string? username, string? password) =>
        SmbConnectResult.Ok(ServiceMessageCodec.Key("Smb.PassThroughSkip"));

    public void TryDisconnect(string shareRootUnc)
    {
    }
}
