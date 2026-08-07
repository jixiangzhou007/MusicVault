namespace MusicVault.Services.Contracts;

/// <summary>应用层统一返回体，包含成功结果与错误语义。</summary>
public sealed record ApplicationResult<T>(
    bool Succeeded,
    T? Data = default,
    ApplicationErrorCode ErrorCode = ApplicationErrorCode.None,
    string? ErrorMessage = null)
{
    public static ApplicationResult<T> Success(T data) => new(true, data);

    public static ApplicationResult<T> Failure(ApplicationErrorCode code, string message) =>
        new(false, default, code, message);
}
