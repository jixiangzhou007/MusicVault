namespace MusicVault.App.Services;

public enum HomeEmptyPrimaryKind
{
    AddLibrary,
    OpenTracks,
    Discover,
    Dual
}

/// <summary>空首页主按钮规则（纯函数）。</summary>
public static class HomeEmptyCta
{
    public static HomeEmptyPrimaryKind Resolve(FirstRunOnboardingPath path, bool hasLibrary) =>
        path switch
        {
            FirstRunOnboardingPath.Discover => HomeEmptyPrimaryKind.Discover,
            FirstRunOnboardingPath.LocalLibrary when hasLibrary => HomeEmptyPrimaryKind.OpenTracks,
            FirstRunOnboardingPath.LocalLibrary => HomeEmptyPrimaryKind.AddLibrary,
            _ when hasLibrary => HomeEmptyPrimaryKind.OpenTracks,
            _ => HomeEmptyPrimaryKind.Dual
        };

    /// <summary>空首页「下载任务」次要按钮：仅在有进行中或失败任务时出现。</summary>
    public static bool ShowDownloadsSecondary(bool hasActiveOrFailedDownloads) =>
        hasActiveOrFailedDownloads;
}
