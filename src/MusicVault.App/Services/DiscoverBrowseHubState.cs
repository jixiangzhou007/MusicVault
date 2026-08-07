namespace MusicVault.App.Services;

/// <summary>Discover browse-hub vs search-session visibility (spec 2026-07-31).</summary>
public static class DiscoverBrowseHubState
{
    /// <param name="isBusy">IsSearching || IsParsingLink || IsRecognizing</param>
    public static bool ShowBrowseHub(
        bool isSearchMode,
        bool hasActiveSearchSession,
        bool isBusy) =>
        isSearchMode && !hasActiveSearchSession && !isBusy;

    public static bool UseHeroSearch(bool showBrowseHub) => showBrowseHub;
}
