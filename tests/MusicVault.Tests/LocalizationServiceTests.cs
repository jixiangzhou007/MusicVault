using MusicVault.App.Localization;
using MusicVault.App.Services;

namespace MusicVault.Tests;

public sealed class LocalizationServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly AppSettingsService _settings;

    public LocalizationServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "MusicVault.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _settings = new AppSettingsService(_dir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, recursive: true);
        }
        catch
        {
            /* best effort */
        }
    }

    [Fact]
    public void Get_returns_zh_string_by_default()
    {
        var l10n = new LocalizationService(_settings);
        Assert.Equal("曲库", l10n.Get("Nav.Library"));
        Assert.Equal(LocalizationService.ZhCn, l10n.Culture);
    }

    [Fact]
    public async Task SetCulture_switches_to_en_and_persists()
    {
        var l10n = new LocalizationService(_settings);
        var changed = false;
        l10n.CultureChanged += (_, _) => changed = true;

        await l10n.SetCultureAsync(LocalizationService.EnUs);

        Assert.Equal("Library", l10n.Get("Nav.Library"));
        Assert.Equal(LocalizationService.EnUs, _settings.Current.Culture);
        Assert.True(changed);
    }

    [Fact]
    public void Get_unknown_key_returns_key()
    {
        var l10n = new LocalizationService(_settings);
        Assert.Equal("Missing.Key", l10n.Get("Missing.Key"));
    }

    [Fact]
    public void NormalizeCulture_falls_back_to_zh_for_unknown()
    {
        Assert.Equal(LocalizationService.ZhCn, LocalizationService.NormalizeCulture("fr-FR"));
        Assert.Equal(LocalizationService.EnUs, LocalizationService.NormalizeCulture("en-US"));
    }

    [Fact]
    public async Task SetCulture_switches_discover_strings()
    {
        var l10n = new LocalizationService(_settings);
        await l10n.SetCultureAsync(LocalizationService.EnUs);
        Assert.Equal("Sources", l10n.Get("Discover.PageTitle"));
        Assert.Equal("Add all ({0})", l10n.Get("Discover.BatchEnqueue"));
    }

    [Fact]
    public async Task SetCulture_switches_library_and_player_strings()
    {
        var l10n = new LocalizationService(_settings);
        await l10n.SetCultureAsync(LocalizationService.EnUs);
        Assert.Equal("Not playing", l10n.Get("Player.NotPlaying"));
        Assert.Equal("Volume", l10n.Get("Player.Volume"));
        Assert.Equal("Switch library", l10n.Get("Library.SwitchLibrary"));
        Assert.Equal("Albums", l10n.Get("Library.Albums.Title"));
        Assert.Equal("Artists", l10n.Get("Library.Artists.Title"));
        Assert.Equal("Liked", l10n.Get("Library.Favorites"));
    }
}
