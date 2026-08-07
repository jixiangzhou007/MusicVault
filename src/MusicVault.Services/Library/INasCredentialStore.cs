namespace MusicVault.Services.Library;

public sealed record NasStoredCredential(string Username, string Password);

/// <summary>NAS SMB 账号密码安全存储（Windows DPAPI）。</summary>
public interface INasCredentialStore
{
    void Save(string credentialKey, string username, string password);

    NasStoredCredential? TryLoad(string credentialKey);

    void Delete(string credentialKey);
}
