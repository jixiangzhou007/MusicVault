namespace MusicVault.App.Services;

/// <summary>首次向导选择的到达路径。缺省 / 旧配置 = Unset。</summary>
public enum FirstRunOnboardingPath
{
    Unset = 0,
    LocalLibrary = 1,
    Discover = 2
}
