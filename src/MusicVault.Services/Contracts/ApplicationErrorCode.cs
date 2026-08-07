namespace MusicVault.Services.Contracts;

/// <summary>应用层统一错误语义代码。</summary>
public enum ApplicationErrorCode
{
    None = 0,
    NotFound = 1,
    ValidationFailed = 2,
    Conflict = 3,
    InfrastructureFailure = 4,
    ProviderUnavailable = 5,
    Unknown = 99
}
