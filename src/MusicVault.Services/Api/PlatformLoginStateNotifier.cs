namespace MusicVault.Services.Api;

/// <summary>平台 Cookie / 登录状态变更时通知 UI 刷新（如发现页平台 chip）。</summary>
public static class PlatformLoginStateNotifier
{
    public static event EventHandler? Changed;

    public static void NotifyChanged() => Changed?.Invoke(null, EventArgs.Empty);
}
