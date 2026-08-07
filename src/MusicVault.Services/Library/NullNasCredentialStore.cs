namespace MusicVault.Services.Library;

/// <summary>非 Windows / 测试环境：不持久化凭据。</summary>
public sealed class NullNasCredentialStore : INasCredentialStore
{
    public void Save(string credentialKey, string username, string password)
    {
    }

    public NasStoredCredential? TryLoad(string credentialKey) => null;

    public void Delete(string credentialKey)
    {
    }
}
