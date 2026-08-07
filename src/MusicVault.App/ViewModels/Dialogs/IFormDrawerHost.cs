namespace MusicVault.App.ViewModels.Dialogs;

/// <summary>右侧 Form Drawer 完成/关闭契约（大表单，非居中 Modal）。</summary>
public interface IFormDrawerHost
{
    bool DismissOnScrimPress { get; }

    void Complete(object? result);
}
