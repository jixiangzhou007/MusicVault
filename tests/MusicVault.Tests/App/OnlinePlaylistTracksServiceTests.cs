using MusicVault.App.Services;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;

namespace MusicVault.Tests.App;

public sealed class OnlinePlaylistTracksServiceTests
{
    [Fact]
    public async Task AddManyAsync_persists_unique_tracks_in_order()
    {
        var dir = Path.Combine(Path.GetTempPath(), "MusicVault.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var settings = new AppSettingsService(dir);
            var service = new OnlinePlaylistTracksService(settings);
            var tracks = new[]
            {
                new OnlineTrack { Id = "1", Platform = MusicPlatform.NetEase, Title = "A" },
                new OnlineTrack { Id = "2", Platform = MusicPlatform.NetEase, Title = "B" },
                new OnlineTrack { Id = "1", Platform = MusicPlatform.NetEase, Title = "A-dup" }
            };

            var added = await service.AddManyAsync(42, tracks);

            Assert.Equal(2, added);
            var stored = service.GetAll(42);
            Assert.NotEmpty(stored);
            Assert.Equal(new[] { "1", "2" }, stored.Select(t => t.Id).ToArray());
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task GetAll_after_settings_reload_is_not_empty()
    {
        var dir = Path.Combine(Path.GetTempPath(), "MusicVault.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var settings = new AppSettingsService(dir);
            var service = new OnlinePlaylistTracksService(settings);
            await service.AddManyAsync(
                7,
                [new OnlineTrack { Id = "n", Platform = MusicPlatform.NetEase, Title = "听" }]);

            var reloaded = new AppSettingsService(dir);
            await reloaded.LoadAsync();
            var afterLoad = new OnlinePlaylistTracksService(reloaded).GetAll(7);

            Assert.NotEmpty(afterLoad);
            Assert.Equal("n", afterLoad[0].Id);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task AddManyAsync_empty_input_does_not_create_list()
    {
        var dir = Path.Combine(Path.GetTempPath(), "MusicVault.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var settings = new AppSettingsService(dir);
            var service = new OnlinePlaylistTracksService(settings);

            var added = await service.AddManyAsync(1, []);

            Assert.Equal(0, added);
            Assert.Empty(service.GetAll(1));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* ignore */ }
        }
    }
}
