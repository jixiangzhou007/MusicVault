namespace MusicVault.App.Services;

/// <summary>顶栏轻量反馈（成功/错误/信息），约 2–3 秒自动消失；可与 ShellStatus 并存。</summary>
public interface IMessageService
{
    void ShowSuccess(string message, TimeSpan? duration = null);
    void ShowError(string message, TimeSpan? duration = null);
    void ShowInfo(string message, TimeSpan? duration = null);
    void ShowWarning(string message, TimeSpan? duration = null);

    void Dismiss(Guid id);
    void DismissAll();
}
