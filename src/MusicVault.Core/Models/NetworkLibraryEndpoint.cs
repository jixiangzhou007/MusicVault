using System.Text.Json;
using System.Text.Json.Serialization;
using MusicVault.Core.Enums;

namespace MusicVault.Core.Models;

/// <summary>远程 NAS / 网络共享连接配置（序列化存入 Libraries.RemoteEndpointJson）</summary>
public sealed class NetworkLibraryEndpoint
{
    public NetworkLibraryProtocol Protocol { get; set; } = NetworkLibraryProtocol.Smb;

    /// <summary>WebDAV 是否使用 HTTPS（SMB 时忽略）。</summary>
    public bool UseTls { get; set; } = true;

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 445;
    public string ShareName { get; set; } = string.Empty;
    public string SubPath { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;

    /// <summary>连接时内存态；持久化时清空。旧库 JSON 中可能仍含明文，启动时会迁移至凭据库。</summary>
    public string Password { get; set; } = string.Empty;

    public bool RememberCredentials { get; set; } = true;

    /// <summary>DPAPI 凭据文件键（不含密码明文）。</summary>
    public string? CredentialKey { get; set; }

    public string BuildUncRootPath()
    {
        var host = Host.Trim();
        var share = ShareName.Trim().Trim('\\', '/');
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(share))
            return string.Empty;

        var root = $@"\\{host}\{share}";
        var sub = SubPath.Trim().Trim('\\', '/');
        return string.IsNullOrWhiteSpace(sub) ? root : Path.Combine(root, sub);
    }

    public string BuildWebDavRootPath()
    {
        var host = Host.Trim();
        if (string.IsNullOrWhiteSpace(host))
            return string.Empty;

        var scheme = UseTls ? "https" : "http";
        var defaultPort = UseTls ? 443 : 80;
        var port = Port > 0 ? Port : defaultPort;
        var portSegment = port == defaultPort ? string.Empty : $":{port}";
        var baseUrl = $"{scheme}://{host}{portSegment}";

        var sub = SubPath.Trim().Trim('\\', '/');
        if (string.IsNullOrWhiteSpace(sub))
            return baseUrl.TrimEnd('/') + "/";

        return $"{baseUrl.TrimEnd('/')}/{sub}/";
    }

    public string BuildRootPath() =>
        Protocol == NetworkLibraryProtocol.WebDav ? BuildWebDavRootPath() : BuildUncRootPath();

    public string BuildShareRootPath() => Nas.NasPathHelper.GetShareRoot(BuildUncRootPath());

    public NetworkLibraryEndpoint Clone() => new()
    {
        Protocol = Protocol,
        UseTls = UseTls,
        Host = Host,
        Port = Port,
        ShareName = ShareName,
        SubPath = SubPath,
        Username = Username,
        Password = Password,
        RememberCredentials = RememberCredentials,
        CredentialKey = CredentialKey
    };

    public static NetworkLibraryEndpoint? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<NetworkLibraryEndpoint>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>写入数据库：不含密码明文。</summary>
    public string ToPersistedJson()
    {
        var persisted = Clone();
        persisted.Password = string.Empty;
        return JsonSerializer.Serialize(persisted, JsonOptions);
    }

    public string ToJson() => ToPersistedJson();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };
}
