using CommunityToolkit.Mvvm.ComponentModel;
using MusicVault.Core.Models;

namespace MusicVault.App.ViewModels.Dialogs;

/// <summary>主窗口右侧 Form Drawer（宽表单/表格；与歌词/元数据侧栏同 Motion，独立层叠）。</summary>
public partial class FormDrawerHostViewModel : ViewModelBase, IFormDrawerHost
{
    private readonly Action? _onOpening;
    private TaskCompletionSource<object?>? _tcs;

    public FormDrawerHostViewModel(Action? onOpening = null) => _onOpening = onOpening;

    [ObservableProperty] private bool _isOpen;

    [ObservableProperty] private object? _content;

    [ObservableProperty] private double _panelWidth = 560;

    [ObservableProperty] private double _panelMaxWidth = 560;

    [ObservableProperty] private bool _isWidePanel;

    public bool DismissOnScrimPress { get; private set; } = true;

    private const double StandardPanelWidth = 560;
    private const double CombinedLabelsPanelWidth = 1040;

    public async Task ShowMusicApiAccountAsync(string? preferredSource = null)
    {
        DismissOnScrimPress = true;
        ApplyStandardPanelLayout(420);
        var vm = new MusicApiAccountModalViewModel(this, preferredSource);
        await ShowReferenceAsync<object>(vm).ConfigureAwait(true);
    }

    public Task<OnlineTrack?> ShowCombinedLabelsSearchAsync(CombinedLabelsSearchViewModel vm)
    {
        DismissOnScrimPress = true;
        ApplyCombinedLabelsPanelLayout();
        return ShowReferenceAsync<OnlineTrack>(vm);
    }

    public Task<MetadataLyricsEditorResult?> ShowMetadataLyricsEditorAsync(MetadataLyricsEditorModalViewModel vm)
    {
        DismissOnScrimPress = true;
        ApplyStandardPanelLayout(StandardPanelWidth);
        return ShowReferenceAsync<MetadataLyricsEditorResult>(vm);
    }

    private void ApplyStandardPanelLayout(double width)
    {
        IsWidePanel = false;
        PanelMaxWidth = StandardPanelWidth;
        PanelWidth = width;
    }

    private void ApplyCombinedLabelsPanelLayout()
    {
        IsWidePanel = true;
        PanelMaxWidth = CombinedLabelsPanelWidth;
        PanelWidth = CombinedLabelsPanelWidth;
    }

    public async Task<T?> ShowReferenceAsync<T>(object content) where T : class
    {
        if (IsOpen)
            throw new InvalidOperationException(App.Localization.Get("App.Drawer.AlreadyOpen"));

        _onOpening?.Invoke();
        Content = content;
        IsOpen = true;
        _tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

        var result = await _tcs.Task.ConfigureAwait(true);
        return result as T;
    }

    public void Complete(object? result)
    {
        if (!IsOpen)
            return;

        IsOpen = false;
        Content = null;
        ApplyStandardPanelLayout(StandardPanelWidth);
        _tcs?.TrySetResult(result);
        _tcs = null;
    }

    public void DismissFromScrim()
    {
        if (!DismissOnScrimPress || !IsOpen)
            return;

        Complete(null);
    }
}
