using System.Collections.Concurrent;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Core.Nas;
using MusicVault.Data.Repositories;
using MusicVault.Services;

namespace MusicVault.Services.Library;

/// <summary>WebDAV 远程音乐库：凭据、连接测试、可达性状态与注册。</summary>
public sealed class WebDavLibraryService
{
    private readonly LibraryRepository _libraries;
    private readonly INasCredentialStore _credentials;
    private readonly WebDavClient _webDav;
    private readonly ConcurrentDictionary<long, NasLibraryStatus> _statusByLibraryId = new();

    public WebDavMountSummary? LastMountSummary { get; private set; }

    public event EventHandler? StatusChanged;

    public WebDavLibraryService(
        LibraryRepository libraries,
        INasCredentialStore credentials,
        WebDavClient webDav)
    {
        _libraries = libraries;
        _credentials = credentials;
        _webDav = webDav;
    }

    public NasLibraryStatus? GetStatus(long libraryId) =>
        _statusByLibraryId.TryGetValue(libraryId, out var status) ? status : null;

    public NasConnectionTestResult TestConnection(NetworkLibraryEndpoint endpoint) =>
        _webDav.TestAsync(endpoint).GetAwaiter().GetResult();

    public async Task<NasConnectionTestResult> TestConnectionAsync(
        NetworkLibraryEndpoint endpoint,
        CancellationToken ct = default) =>
        await _webDav.TestAsync(endpoint, ct).ConfigureAwait(false);

    public async Task<NasConnectionTestResult> ReconnectLibraryAsync(long libraryId, CancellationToken ct = default)
    {
        var endpoint = await ResolveStoredEndpointAsync(libraryId, ct).ConfigureAwait(false);
        if (endpoint == null)
            return NasConnectionTestResult.Fail(ServiceMessageCodec.Key("WebDav.NotWebDavLibrary"));

        var result = await _webDav.TestAsync(endpoint, ct).ConfigureAwait(false);
        SetReachability(libraryId, result.Success ? NasReachability.Online : NasReachability.Offline, result.Message);
        return result;
    }

    public async Task<(MusicLibrary Library, bool Created)> AddOrGetWebDavLibraryAsync(
        string displayName,
        NetworkLibraryEndpoint endpoint,
        CancellationToken ct = default)
    {
        endpoint.Protocol = NetworkLibraryProtocol.WebDav;
        if (endpoint.Port <= 0)
            endpoint.Port = endpoint.UseTls ? 443 : 80;

        var test = await _webDav.TestAsync(endpoint, ct).ConfigureAwait(false);
        if (!test.Success)
            throw new InvalidOperationException(test.Message);

        var root = LibraryRepository.NormalizeRootPath(endpoint.BuildWebDavRootPath());
        var existing = await _libraries.GetByRootPathAsync(root, ct).ConfigureAwait(false);
        if (existing != null)
        {
            await PersistCredentialsForLibraryAsync(existing, endpoint, ct).ConfigureAwait(false);
            SetReachability(existing.Id, NasReachability.Online, test.Message);
            return (existing, false);
        }

        var lib = new MusicLibrary
        {
            Name = string.IsNullOrWhiteSpace(displayName) ? endpoint.Host : displayName.Trim(),
            RootPath = root,
            SourceType = LibrarySourceType.NetworkWebDav,
            RemoteEndpointJson = endpoint.ToPersistedJson(),
            IsEnabled = true
        };

        lib.Id = await _libraries.AddAsync(lib, ct).ConfigureAwait(false);
        await PersistCredentialsForLibraryAsync(lib, endpoint, ct).ConfigureAwait(false);
        SetReachability(lib.Id, NasReachability.Online, test.Message);
        return (lib, true);
    }

    public async Task<WebDavMountSummary> MountAllEnabledLibrariesAsync(CancellationToken ct = default)
    {
        var summary = new WebDavMountSummary();
        var libs = await _libraries.GetAllAsync(ct).ConfigureAwait(false);
        foreach (var lib in libs.Where(l => l.IsEnabled && l.SourceType == LibrarySourceType.NetworkWebDav))
        {
            var endpoint = await ResolveStoredEndpointAsync(lib.Id, ct).ConfigureAwait(false);
            if (endpoint == null)
            {
                SetReachability(lib.Id, NasReachability.Offline, ServiceMessageCodec.Key("Nas.InvalidConfig"));
                summary.Failed.Add(new NasMountFailure(lib.Name, ServiceMessageCodec.Key("Nas.InvalidConfig")));
                continue;
            }

            if (await MigrateLegacyCredentialsIfNeededAsync(lib, endpoint, ct).ConfigureAwait(false))
                endpoint = await ResolveStoredEndpointAsync(lib.Id, ct).ConfigureAwait(false);

            var result = await _webDav.TestAsync(endpoint!, ct).ConfigureAwait(false);
            if (result.Success)
            {
                SetReachability(lib.Id, NasReachability.Online, result.Message);
                summary.Succeeded.Add(lib.Name);
            }
            else
            {
                SetReachability(lib.Id, NasReachability.Offline, result.Message);
                summary.Failed.Add(new NasMountFailure(lib.Name, result.Message));
            }
        }

        LastMountSummary = summary;
        return summary;
    }

    public async Task<NetworkLibraryEndpoint?> ResolveStoredEndpointAsync(long libraryId, CancellationToken ct = default)
    {
        var lib = await _libraries.GetByIdAsync(libraryId, ct).ConfigureAwait(false);
        if (lib?.SourceType != LibrarySourceType.NetworkWebDav)
            return null;

        var endpoint = NetworkLibraryEndpoint.TryParse(lib.RemoteEndpointJson);
        return endpoint == null ? null : ResolveCredentials(endpoint);
    }

    private void SetReachability(long libraryId, NasReachability reachability, string message)
    {
        _statusByLibraryId[libraryId] = new NasLibraryStatus(
            libraryId,
            reachability,
            message,
            DateTime.UtcNow);
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private NetworkLibraryEndpoint ResolveCredentials(NetworkLibraryEndpoint endpoint)
    {
        if (!string.IsNullOrWhiteSpace(endpoint.Password))
            return endpoint;

        if (string.IsNullOrWhiteSpace(endpoint.CredentialKey))
            return endpoint;

        var stored = _credentials.TryLoad(endpoint.CredentialKey);
        if (stored == null)
            return endpoint;

        var resolved = endpoint.Clone();
        resolved.Username = stored.Username;
        resolved.Password = stored.Password;
        return resolved;
    }

    private async Task PersistCredentialsForLibraryAsync(
        MusicLibrary library,
        NetworkLibraryEndpoint endpoint,
        CancellationToken ct)
    {
        var persisted = endpoint.Clone();
        var hasInlinePassword = !string.IsNullOrEmpty(persisted.Password);
        var shouldRemember = persisted.RememberCredentials
            && (!string.IsNullOrWhiteSpace(persisted.Username) || hasInlinePassword);

        if (shouldRemember)
        {
            var key = NasCredentialKeys.ForLibrary(library.Id);
            _credentials.Save(key, persisted.Username, persisted.Password);
            persisted.CredentialKey = key;
        }
        else if (!string.IsNullOrWhiteSpace(persisted.CredentialKey))
        {
            _credentials.Delete(persisted.CredentialKey);
            persisted.CredentialKey = null;
        }

        persisted.Password = string.Empty;
        library.RemoteEndpointJson = persisted.ToPersistedJson();
        await _libraries.UpdateAsync(library, ct).ConfigureAwait(false);
    }

    private async Task<bool> MigrateLegacyCredentialsIfNeededAsync(
        MusicLibrary library,
        NetworkLibraryEndpoint endpoint,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(endpoint.Password) || !string.IsNullOrWhiteSpace(endpoint.CredentialKey))
            return false;

        endpoint.RememberCredentials = true;
        await PersistCredentialsForLibraryAsync(library, endpoint, ct).ConfigureAwait(false);
        return true;
    }
}

public sealed class WebDavMountSummary
{
    public List<string> Succeeded { get; } = [];
    public List<NasMountFailure> Failed { get; } = [];
}
