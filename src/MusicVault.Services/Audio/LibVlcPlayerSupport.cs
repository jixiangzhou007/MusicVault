using System.Runtime.InteropServices;

namespace MusicVault.Services.Audio;

/// <summary>LibVLC 运行时与打包可用性探测。</summary>
internal static class LibVlcPlayerSupport
{
    /// <summary>
    /// Windows / macOS：有旁路打包路径时可尝试 LibVLC。
    /// Linux：Phase 3 仍走系统包 + <c>LD_LIBRARY_PATH</c>，不视为已旁路打包。
    /// </summary>
    public static bool IsPackagedForCurrentPlatform =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();

    public static string? TryResolveNativeRoot() =>
        TryResolveNativeRootFrom(AppContext.BaseDirectory);

    /// <summary>测试与发布脚本共用的探测逻辑（不依赖 <see cref="AppContext.BaseDirectory"/>）。</summary>
    internal static string? TryResolveNativeRootFrom(string baseDir)
    {
        if (string.IsNullOrWhiteSpace(baseDir))
            return null;

        if (OperatingSystem.IsWindows())
        {
            foreach (var sub in new[] { "win-x64", "win-x86", "x64", "x86" })
            {
                var candidate = Path.Combine(baseDir, "libvlc", sub);
                if (LooksLikeLibVlcDirectory(candidate))
                    return candidate;
            }
        }

        // macOS 布局始终探测（与宿主 OS 无关，便于发布验收与单测）
        foreach (var candidate in EnumerateMacCandidates(baseDir))
        {
            if (LooksLikeLibVlcDirectory(candidate))
                return candidate;
        }

        var flat = Path.Combine(baseDir, "libvlc");
        if (LooksLikeLibVlcDirectory(flat))
            return flat;

        // VideoLAN.LibVLC.Mac.targets 可能把 libvlc.dylib 放到输出根目录
        if (LooksLikeLibVlcDirectory(baseDir))
            return baseDir;

        return null;
    }

    internal static bool LooksLikeLibVlcDirectory(string directory)
    {
        if (!Directory.Exists(directory))
            return false;

        return File.Exists(Path.Combine(directory, "libvlc.dll"))
            || File.Exists(Path.Combine(directory, "libvlccore.dll"))
            || File.Exists(Path.Combine(directory, "libvlc.dylib"))
            || File.Exists(Path.Combine(directory, "libvlccore.dylib"))
            || File.Exists(Path.Combine(directory, "libvlc.so"))
            || File.Exists(Path.Combine(directory, "libvlccore.so"));
    }

    private static IEnumerable<string> EnumerateMacCandidates(string baseDir)
    {
        var preferArm = RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
        var primary = preferArm ? "osx-arm64" : "osx-x64";
        var secondary = preferArm ? "osx-x64" : "osx-arm64";
        var primaryAlias = preferArm ? "mac-arm64" : "mac-x64";
        var secondaryAlias = preferArm ? "mac-x64" : "mac-arm64";

        foreach (var plat in new[] { primary, secondary, primaryAlias, secondaryAlias })
        {
            yield return Path.Combine(baseDir, "libvlc", plat, "lib");
            yield return Path.Combine(baseDir, "libvlc", plat);
        }

        yield return Path.Combine(baseDir, "libvlc", "lib");
    }
}
