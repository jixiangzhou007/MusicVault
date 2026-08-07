using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MusicVault.Services.Library;

/// <summary>使用 Windows DPAPI 将 NAS 凭据加密存于数据目录。</summary>
public sealed class DpapiNasCredentialStore : INasCredentialStore
{
    private readonly string _credentialsDir;

    public DpapiNasCredentialStore(string dataDirectory)
    {
        _credentialsDir = Path.Combine(dataDirectory, "nas-credentials");
        Directory.CreateDirectory(_credentialsDir);
    }

    public void Save(string credentialKey, string username, string password)
    {
        if (string.IsNullOrWhiteSpace(credentialKey))
            throw new ArgumentException("Credential key is required.", nameof(credentialKey));

        var payload = JsonSerializer.SerializeToUtf8Bytes(new StoredPayload
        {
            Username = username ?? string.Empty,
            Password = password ?? string.Empty
        });

        var protectedBytes = ProtectedData.Protect(payload, optionalEntropy: null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(GetFilePath(credentialKey), protectedBytes);
    }

    public NasStoredCredential? TryLoad(string credentialKey)
    {
        if (string.IsNullOrWhiteSpace(credentialKey))
            return null;

        var path = GetFilePath(credentialKey);
        if (!File.Exists(path))
            return null;

        try
        {
            var protectedBytes = File.ReadAllBytes(path);
            var payloadBytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            var payload = JsonSerializer.Deserialize<StoredPayload>(payloadBytes);
            if (payload == null)
                return null;

            return new NasStoredCredential(payload.Username, payload.Password);
        }
        catch
        {
            return null;
        }
    }

    public void Delete(string credentialKey)
    {
        if (string.IsNullOrWhiteSpace(credentialKey))
            return;

        var path = GetFilePath(credentialKey);
        if (File.Exists(path))
            File.Delete(path);
    }

    private string GetFilePath(string credentialKey)
    {
        var safe = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(credentialKey)));
        return Path.Combine(_credentialsDir, $"{safe}.dpapi");
    }

    private sealed class StoredPayload
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
