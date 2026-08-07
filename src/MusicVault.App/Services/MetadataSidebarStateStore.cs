using MusicVault.Core.Enums;

namespace MusicVault.App.Services;

/// <summary>维护元数据侧栏在跨分区切换时的恢复策略状态。</summary>
public sealed class MetadataSidebarStateStore
{
    private bool _pendingMetadataRestore;
    private bool _autoCloseTriggered;
    private RightPanelMode _lastObservedMode = RightPanelMode.Closed;

    public void Initialize(RightPanelMode initialMode) => _lastObservedMode = initialMode;

    public void MarkMetadataRequested() => _pendingMetadataRestore = true;

    public bool TryResolveModeForSection(
        NavigationSection section,
        RightPanelMode currentMode,
        out RightPanelMode nextMode)
    {
        nextMode = currentMode;
        if (SupportsMetadataSidebar(section))
        {
            if (_pendingMetadataRestore && currentMode != RightPanelMode.Metadata)
            {
                nextMode = RightPanelMode.Metadata;
                return true;
            }

            return false;
        }

        if (currentMode == RightPanelMode.Metadata)
        {
            _pendingMetadataRestore = true;
            _autoCloseTriggered = true;
            nextMode = RightPanelMode.Closed;
            return true;
        }

        return false;
    }

    public void OnModeObserved(RightPanelMode currentMode)
    {
        var previous = _lastObservedMode;
        _lastObservedMode = currentMode;

        if (previous == RightPanelMode.Metadata && currentMode == RightPanelMode.Closed)
        {
            if (_autoCloseTriggered)
            {
                _autoCloseTriggered = false;
                return;
            }

            _pendingMetadataRestore = false;
        }
    }

    private static bool SupportsMetadataSidebar(NavigationSection section) =>
        section is NavigationSection.AllTracks
            or NavigationSection.Albums
            or NavigationSection.Artists;
}
