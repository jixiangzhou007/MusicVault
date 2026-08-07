using Avalonia.Controls;
using MusicVault.App.Services;
using Xunit;

namespace MusicVault.Tests.App;

public sealed class AppKeyboardShortcutsTests
{
    [Fact]
    public void ShouldSuppressPlaybackShortcuts_WhenButtonFocused()
    {
        Assert.True(AppKeyboardShortcuts.ShouldSuppressPlaybackShortcuts(new Button()));
    }

    [Fact]
    public void ShouldSuppressPlaybackShortcuts_WhenListBoxItemFocused()
    {
        Assert.True(AppKeyboardShortcuts.ShouldSuppressPlaybackShortcuts(new ListBoxItem()));
    }

    [Fact]
    public void ShouldSuppressPlaybackShortcuts_WhenTextBoxFocused()
    {
        Assert.True(AppKeyboardShortcuts.IsTextEntryFocused(null) == false);
        Assert.True(AppKeyboardShortcuts.ShouldSuppressPlaybackShortcuts(new TextBox()));
    }

    [Fact]
    public void ShouldNotSuppressPlaybackShortcuts_WhenNothingFocused()
    {
        Assert.False(AppKeyboardShortcuts.ShouldSuppressPlaybackShortcuts(null));
    }
}
