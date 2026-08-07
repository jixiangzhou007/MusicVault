namespace MusicVault.Core.Platforms;

/// <summary>根据当前操作系统创建平台服务实例</summary>
public static class PlatformServiceFactory
{
    /// <summary>创建当前平台的 IPlatformService 实现</summary>
    public static IPlatformService Create()
    {
        if (OperatingSystem.IsWindows())
            return new WindowsPlatformService();
        if (OperatingSystem.IsMacOS())
            return new MacOSPlatformService();
        return new WindowsPlatformService();
    }
}
