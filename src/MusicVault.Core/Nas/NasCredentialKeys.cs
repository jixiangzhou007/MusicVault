namespace MusicVault.Core.Nas;

/// <summary>NAS 凭据存储键命名。</summary>
public static class NasCredentialKeys
{
    public static string ForLibrary(long libraryId) => $"nas-lib-{libraryId}";
}
