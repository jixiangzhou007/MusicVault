namespace MusicVault.App.Services;

/// <summary>首次向导步进规则（纯逻辑，可供单测）。</summary>
public static class FirstRunWizardFlow
{
    public const int LastStepIndex = 2;

    public static bool CanGoNext(int stepIndex, FirstRunOnboardingPath selectedPath) =>
        stepIndex switch
        {
            0 => true,
            1 => selectedPath is FirstRunOnboardingPath.LocalLibrary or FirstRunOnboardingPath.Discover,
            2 => true,
            _ => false
        };

    public static bool IsWelcome(int stepIndex) => stepIndex == 0;
    public static bool IsPathStep(int stepIndex) => stepIndex == 1;
    public static bool IsDetailStep(int stepIndex) => stepIndex == 2;

    public static bool IsLibraryDetail(int stepIndex, FirstRunOnboardingPath path) =>
        IsDetailStep(stepIndex) && path == FirstRunOnboardingPath.LocalLibrary;

    public static bool IsDiscoverDetail(int stepIndex, FirstRunOnboardingPath path) =>
        IsDetailStep(stepIndex) && path == FirstRunOnboardingPath.Discover;

    public static bool ShowSkipDetail(int stepIndex, FirstRunOnboardingPath path) =>
        IsLibraryDetail(stepIndex, path);

    public static DiscoverSetupCopyKind ResolveDiscoverCopy(bool isMacOs, bool hasBundledApi)
    {
        if (isMacOs)
            return DiscoverSetupCopyKind.Mac;
        return hasBundledApi ? DiscoverSetupCopyKind.Bundled : DiscoverSetupCopyKind.Direct;
    }
}

public enum DiscoverSetupCopyKind
{
    Bundled,
    Direct,
    Mac
}
