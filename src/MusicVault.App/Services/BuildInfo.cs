namespace MusicVault.App.Services;

/// <summary>用于确认当前运行的是哪一次发布的程序</summary>
public static class BuildInfo
{
    public static string GetBuildLabel()
    {
        var path = Environment.ProcessPath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return "dev";

        var time = File.GetLastWriteTime(path);
        return time.ToString("yyyy-MM-dd HH:mm");
    }

    public static string WindowTitle => $"MusicVault · {GetBuildLabel()}";
}
