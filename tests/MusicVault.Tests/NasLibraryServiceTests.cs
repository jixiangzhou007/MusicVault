using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Core.Nas;

namespace MusicVault.Tests;

public sealed class NetworkLibraryEndpointTests
{
    [Fact]
    public void BuildUncRootPath_combines_host_share_and_subpath()
    {
        var ep = new NetworkLibraryEndpoint
        {
            Host = "192.168.1.8",
            ShareName = "music",
            SubPath = "Albums/Pop"
        };

        var unc = ep.BuildUncRootPath();
        Assert.Contains(@"\\192.168.1.8\music", unc, StringComparison.Ordinal);
        Assert.Contains("Albums", unc, StringComparison.Ordinal);
    }

    [Fact]
    public void ToPersistedJson_does_not_include_password()
    {
        var ep = new NetworkLibraryEndpoint
        {
            Host = "nas.local",
            ShareName = "audio",
            Username = "dj",
            Password = "secret",
            CredentialKey = "nas-lib-1"
        };

        var json = ep.ToPersistedJson();
        Assert.DoesNotContain("secret", json, StringComparison.Ordinal);
        Assert.Contains("nas-lib-1", json, StringComparison.Ordinal);

        var roundTrip = NetworkLibraryEndpoint.TryParse(json);
        Assert.NotNull(roundTrip);
        Assert.Equal("dj", roundTrip!.Username);
        Assert.Equal(string.Empty, roundTrip.Password);
    }
}

public sealed class NasPathHelperTests
{
    [Fact]
    public void GetShareRoot_extracts_host_and_share()
    {
        var root = NasPathHelper.GetShareRoot(@"\\192.168.0.3\music\Albums\Pop");
        Assert.Equal(@"\\192.168.0.3\music", root);
    }

    [Theory]
    [InlineData(@"\\nas\music\a.flac", true)]
    [InlineData(@"C:\music\a.flac", false)]
    public void IsUncPath_detects_unc_paths(string path, bool expected) =>
        Assert.Equal(expected, NasPathHelper.IsUncPath(path));
}

public sealed class NasCredentialStoreTests : IDisposable
{
    private readonly string _dir;

    public NasCredentialStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "mv_nas_cred_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    [Fact]
    public void Dpapi_roundtrip_when_supported()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var store = new MusicVault.Services.Library.DpapiNasCredentialStore(_dir);
        store.Save("nas-lib-99", "user", "pass123");
        var loaded = store.TryLoad("nas-lib-99");

        Assert.NotNull(loaded);
        Assert.Equal("user", loaded!.Username);
        Assert.Equal("pass123", loaded.Password);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, true);
        }
        catch { /* ignore */ }
    }
}

public sealed class NasLibraryRepositoryTests : IDisposable
{
    private readonly string _dir;
    private readonly string _dbPath;
    private readonly MusicVault.Data.Repositories.LibraryRepository _libraries;

    public NasLibraryRepositoryTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "mv_nas_lib_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _dbPath = Path.Combine(_dir, "test.db");
        MusicVault.Data.Database.DatabaseInitializer.InitializeAsync(_dbPath).GetAwaiter().GetResult();
        _libraries = new MusicVault.Data.Repositories.LibraryRepository(_dbPath);
    }

    [Fact]
    public async Task MountAll_migrates_legacy_password_to_credential_key()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var musicDir = Path.Combine(_dir, "music");
        Directory.CreateDirectory(musicDir);

        var endpoint = new NetworkLibraryEndpoint
        {
            Host = "test",
            ShareName = "music",
            Username = "nas-user",
            Password = "legacy-plain",
            RememberCredentials = true
        };

        var lib = new MusicVault.Core.Models.MusicLibrary
        {
            Name = "Test NAS",
            RootPath = @"\\test\music",
            SourceType = MusicVault.Core.Enums.LibrarySourceType.NetworkNas,
            RemoteEndpointJson = System.Text.Json.JsonSerializer.Serialize(endpoint),
            IsEnabled = true
        };
        lib.Id = await _libraries.AddAsync(lib);

        var cred = new MusicVault.Services.Library.DpapiNasCredentialStore(_dir);
        var smb = new MusicVault.Services.Library.PassThroughSmbShareConnector();
        var nas = new MusicVault.Services.Library.NasLibraryService(_libraries, cred, smb);

        _ = await nas.MountAllEnabledLibrariesAsync();

        var reloaded = await _libraries.GetByIdAsync(lib.Id);
        Assert.NotNull(reloaded);
        var parsed = NetworkLibraryEndpoint.TryParse(reloaded!.RemoteEndpointJson);
        Assert.NotNull(parsed);
        Assert.Equal(MusicVault.Core.Nas.NasCredentialKeys.ForLibrary(lib.Id), parsed!.CredentialKey);
        Assert.Equal(string.Empty, parsed.Password);

        var stored = cred.TryLoad(parsed.CredentialKey!);
        Assert.NotNull(stored);
        Assert.Equal("legacy-plain", stored!.Password);
    }

    [Fact]
    public async Task MountAll_records_offline_status_when_share_unreachable()
    {
        var endpoint = new NetworkLibraryEndpoint
        {
            Host = "192.168.99.99",
            ShareName = "missing",
            RememberCredentials = false
        };

        var lib = new MusicLibrary
        {
            Name = "Unreachable NAS",
            RootPath = @"\\192.168.99.99\missing",
            SourceType = LibrarySourceType.NetworkNas,
            RemoteEndpointJson = endpoint.ToPersistedJson(),
            IsEnabled = true
        };
        lib.Id = await _libraries.AddAsync(lib);

        var cred = new MusicVault.Services.Library.NullNasCredentialStore();
        var smb = new MusicVault.Services.Library.PassThroughSmbShareConnector();
        var nas = new MusicVault.Services.Library.NasLibraryService(_libraries, cred, smb);

        var summary = await nas.MountAllEnabledLibrariesAsync();

        Assert.Single(summary.Failed);
        var status = nas.GetStatus(lib.Id);
        Assert.NotNull(status);
        Assert.Equal(NasReachability.Offline, status!.Reachability);
    }

    [Fact]
    public async Task ReconnectLibraryAsync_updates_status()
    {
        var endpoint = new NetworkLibraryEndpoint
        {
            Host = "192.168.99.98",
            ShareName = "missing",
        };

        var lib = new MusicLibrary
        {
            Name = "NAS",
            RootPath = @"\\192.168.99.98\missing",
            SourceType = LibrarySourceType.NetworkNas,
            RemoteEndpointJson = endpoint.ToPersistedJson(),
            IsEnabled = true
        };
        lib.Id = await _libraries.AddAsync(lib);

        var nas = new MusicVault.Services.Library.NasLibraryService(
            _libraries,
            new MusicVault.Services.Library.NullNasCredentialStore(),
            new MusicVault.Services.Library.PassThroughSmbShareConnector());

        var result = await nas.ReconnectLibraryAsync(lib.Id);
        Assert.False(result.Success);

        var status = nas.GetStatus(lib.Id);
        Assert.NotNull(status);
        Assert.Equal(NasReachability.Offline, status!.Reachability);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, true);
        }
        catch { /* ignore */ }
    }
}
