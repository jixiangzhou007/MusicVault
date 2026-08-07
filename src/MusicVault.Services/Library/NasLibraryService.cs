using System.Collections.Concurrent;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Core.Nas;
using MusicVault.Data.Repositories;
using MusicVault.Services;

namespace MusicVault.Services.Library;

/// <summary>远程 NAS 音乐库：凭据存储、SMB 挂载、连接测试、可达性状态与注册。</summary>
public sealed class NasLibraryService
{
    private readonly LibraryRepository _libraries;
    private readonly INasCredentialStore _credentials;
    private readonly ISmbShareConnector _smb;
    private readonly HashSet<string> _mountedShareRoots = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<long, NasLibraryStatus> _statusByLibraryId = new();

    public NasMountSummary? LastMountSummary { get; private set; }

    public event EventHandler? StatusChanged;

    public NasLibraryService(
        LibraryRepository libraries,
        INasCredentialStore credentials,
        ISmbShareConnector smb)
    {
        _libraries = libraries;
        _credentials = credentials;
        _smb = smb;
    }

    public NasLibraryStatus? GetStatus(long libraryId) =>
        _statusByLibraryId.TryGetValue(libraryId, out var status) ? status : null;

    public IReadOnlyDictionary<long, NasLibraryStatus> GetAllStatuses() => _statusByLibraryId;

    public NasConnectionTestResult TestConnection(NetworkLibraryEndpoint endpoint) =>
        EnsureAccessible(endpoint);

    public async Task<NasConnectionTestResult> ReconnectLibraryAsync(long libraryId, CancellationToken ct = default)
    {
        var lib = await _libraries.GetByIdAsync(libraryId, ct).ConfigureAwait(false);
        if (lib == null || lib.SourceType != LibrarySourceType.NetworkNas)
            return NasConnectionTestResult.Fail(ServiceMessageCodec.Key("Nas.NotNasLibrary"));

        var endpoint = NetworkLibraryEndpoint.TryParse(lib.RemoteEndpointJson);
        if (endpoint == null)
        {
            SetReachability(libraryId, NasReachability.Offline, ServiceMessageCodec.Key("Nas.InvalidConfig"));
            return NasConnectionTestResult.Fail(ServiceMessageCodec.Key("Nas.InvalidConfig"));
        }

        var result = EnsureAccessible(ResolveCredentials(endpoint));
        SetReachability(libraryId, result.Success ? NasReachability.Online : NasReachability.Offline, result.Message);
        return result;
    }

    public async Task<(MusicLibrary Library, bool Created)> AddOrGetNasLibraryAsync(
        string displayName,
        NetworkLibraryEndpoint endpoint,
        CancellationToken ct = default)
    {
        var access = EnsureAccessible(endpoint);
        if (!access.Success)
            throw new InvalidOperationException(access.Message);

        var unc = LibraryRepository.NormalizeRootPath(endpoint.BuildUncRootPath());
        var existing = await _libraries.GetByRootPathAsync(unc, ct).ConfigureAwait(false);
        if (existing != null)
        {
            await PersistCredentialsForLibraryAsync(existing, endpoint, ct).ConfigureAwait(false);
            SetReachability(existing.Id, NasReachability.Online, access.Message);
            return (existing, false);
        }

        var lib = new MusicLibrary
        {
            Name = string.IsNullOrWhiteSpace(displayName) ? endpoint.Host : displayName.Trim(),
            RootPath = unc,
            SourceType = LibrarySourceType.NetworkNas,
            RemoteEndpointJson = endpoint.ToPersistedJson(),
            IsEnabled = true
        };

        lib.Id = await _libraries.AddAsync(lib, ct).ConfigureAwait(false);
        await PersistCredentialsForLibraryAsync(lib, endpoint, ct).ConfigureAwait(false);
        SetReachability(lib.Id, NasReachability.Online, access.Message);
        return (lib, true);
    }

    /// <summary>启动后挂载全部已启用 NAS 库（迁移旧明文密码并尝试 SMB 连接）。</summary>
    public async Task<NasMountSummary> MountAllEnabledLibrariesAsync(CancellationToken ct = default)
    {
        var summary = new NasMountSummary();
        var libs = await _libraries.GetAllAsync(ct).ConfigureAwait(false);
        foreach (var lib in libs.Where(l => l.IsEnabled && l.SourceType == LibrarySourceType.NetworkNas))
        {
            var endpoint = NetworkLibraryEndpoint.TryParse(lib.RemoteEndpointJson);
            if (endpoint == null)
            {
                SetReachability(lib.Id, NasReachability.Offline, ServiceMessageCodec.Key("Nas.InvalidConfig"));
                summary.Failed.Add(new NasMountFailure(lib.Name, ServiceMessageCodec.Key("Nas.InvalidConfig")));
                continue;
            }

            if (await MigrateLegacyCredentialsIfNeededAsync(lib, endpoint, ct).ConfigureAwait(false))
                endpoint = NetworkLibraryEndpoint.TryParse(lib.RemoteEndpointJson) ?? endpoint;

            var result = EnsureAccessible(ResolveCredentials(endpoint));
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

    public void DisconnectMountedShares()
    {
        foreach (var root in _mountedShareRoots.ToList())
            _smb.TryDisconnect(root);
        _mountedShareRoots.Clear();
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

    private NasConnectionTestResult EnsureAccessible(NetworkLibraryEndpoint endpoint)
    {
        var unc = endpoint.BuildUncRootPath();
        if (string.IsNullOrWhiteSpace(unc))
            return NasConnectionTestResult.Fail(ServiceMessageCodec.Key("Nas.HostShareRequired"));

        var resolved = ResolveCredentials(endpoint);
        var shareRoot = resolved.BuildShareRootPath();
        if (!string.IsNullOrWhiteSpace(shareRoot))
        {
            var mount = _smb.TryConnect(shareRoot, NullIfEmpty(resolved.Username), resolved.Password);
            if (!mount.Success)
            {
                return NasConnectionTestResult.Fail(
                    ServiceMessageCodec.Format("Nas.SmbMountFailed", mount.Message));
            }

            _mountedShareRoots.Add(shareRoot);
        }

        try
        {
            if (Directory.Exists(unc))
                return NasConnectionTestResult.Ok(unc, ServiceMessageCodec.Key("Nas.ShareAccessible"));

            return NasConnectionTestResult.Fail(
                ServiceMessageCodec.Format("Nas.ShareInaccessible", unc));
        }
        catch (Exception ex)
        {
            return NasConnectionTestResult.Fail(ServiceMessageCodec.Format("Nas.TestFailed", ex.Message));
        }
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

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record NasConnectionTestResult(bool Success, string Message, string? UncPath = null)
{
    public static NasConnectionTestResult Ok(string unc, string message) =>
        new(true, message, unc);

    public static NasConnectionTestResult Fail(string message) =>
        new(false, message);
}

public sealed class NasMountSummary
{
    public List<string> Succeeded { get; } = [];
    public List<NasMountFailure> Failed { get; } = [];
}

public sealed record NasMountFailure(string LibraryName, string Message);
