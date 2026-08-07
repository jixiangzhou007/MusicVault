using System.ComponentModel;
using System.Runtime.InteropServices;
using MusicVault.Services;

namespace MusicVault.Services.Library;

/// <summary>Windows SMB 挂载：WNetAddConnection2 / WNetCancelConnection2。</summary>
public sealed class WindowsSmbShareConnector : ISmbShareConnector
{
    private const int ResourceTypeDisk = 0x00000001;
    private const int ConnectTemporary = 0x00000004;
    private const int ErrorAlreadyAssigned = 85;
    private const int ErrorDeviceAlreadyRemembered = 1202;
    private const int ErrorSessionCredentialConflict = 1219;

    public SmbConnectResult TryConnect(string shareRootUnc, string? username, string? password)
    {
        if (string.IsNullOrWhiteSpace(shareRootUnc))
            return SmbConnectResult.Fail(ServiceMessageCodec.Key("Smb.SharePathEmpty"));

        if (!OperatingSystem.IsWindows())
            return SmbConnectResult.Ok(ServiceMessageCodec.Key("Smb.SkippedNonWindows"));

        var needsCredentials = !string.IsNullOrWhiteSpace(username) || !string.IsNullOrWhiteSpace(password);
        if (!needsCredentials)
            return SmbConnectResult.Ok(ServiceMessageCodec.Key("Smb.NoCredentialsSession"));

        var resource = new NetResource
        {
            Scope = 0,
            Type = ResourceTypeDisk,
            DisplayType = 0,
            Usage = 0,
            LocalName = null,
            RemoteName = shareRootUnc,
            Comment = null,
            Provider = null
        };

        var user = string.IsNullOrWhiteSpace(username) ? null : username.Trim();
        var pass = string.IsNullOrWhiteSpace(password) ? null : password;
        var result = WNetAddConnection2(ref resource, pass, user, ConnectTemporary);
        if (result == 0
            || result == ErrorAlreadyAssigned
            || result == ErrorDeviceAlreadyRemembered
            || result == ErrorSessionCredentialConflict)
        {
            return SmbConnectResult.Ok(ServiceMessageCodec.Key("Smb.Mounted"));
        }

        return SmbConnectResult.Fail(FormatWin32Message(result), result);
    }

    public void TryDisconnect(string shareRootUnc)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(shareRootUnc))
            return;

        _ = WNetCancelConnection2(shareRootUnc, 0, true);
    }

    private static string FormatWin32Message(int errorCode)
    {
        try
        {
            return new Win32Exception(errorCode).Message;
        }
        catch
        {
            return ServiceMessageCodec.Format("Smb.Win32Error", errorCode);
        }
    }

    [DllImport("mpr.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int WNetAddConnection2(ref NetResource netResource, string? password, string? username, int flags);

    [DllImport("mpr.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int WNetCancelConnection2(string name, int flags, bool force);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NetResource
    {
        public int Scope;
        public int Type;
        public int DisplayType;
        public int Usage;
        public string? LocalName;
        public string? RemoteName;
        public string? Comment;
        public string? Provider;
    }
}
