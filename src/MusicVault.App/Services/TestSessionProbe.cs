namespace MusicVault.App.Services;

/// <summary>Headless / 自动化测试会话探测（避免网络探测阻塞 UI 冒烟）。</summary>
public static class TestSessionProbe
{
    public const string TestDataDirEnvVar = "MUSICVAULT_TEST_DATA_DIR";

    public static bool IsActive =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(TestDataDirEnvVar));
}
