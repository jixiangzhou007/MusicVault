using MusicVault.App.Services;
using MusicVault.Core.Enums;

namespace MusicVault.Tests.App;

public sealed class MetadataSidebarStateStoreTests
{
    [Fact]
    public void Leaving_library_section_auto_closes_metadata_and_marks_restore()
    {
        var store = new MetadataSidebarStateStore();
        store.Initialize(RightPanelMode.Metadata);

        var changed = store.TryResolveModeForSection(
            NavigationSection.Settings,
            RightPanelMode.Metadata,
            out var nextMode);

        Assert.True(changed);
        Assert.Equal(RightPanelMode.Closed, nextMode);
    }

    [Fact]
    public void Returning_to_library_section_restores_metadata_if_pending()
    {
        var store = new MetadataSidebarStateStore();
        store.Initialize(RightPanelMode.Closed);
        store.MarkMetadataRequested();

        var changed = store.TryResolveModeForSection(
            NavigationSection.AllTracks,
            RightPanelMode.Closed,
            out var nextMode);

        Assert.True(changed);
        Assert.Equal(RightPanelMode.Metadata, nextMode);
    }

    [Fact]
    public void Manual_close_clears_pending_restore()
    {
        var store = new MetadataSidebarStateStore();
        store.Initialize(RightPanelMode.Metadata);
        store.MarkMetadataRequested();

        store.OnModeObserved(RightPanelMode.Closed);

        var changed = store.TryResolveModeForSection(
            NavigationSection.Artists,
            RightPanelMode.Closed,
            out _);

        Assert.False(changed);
    }
}
