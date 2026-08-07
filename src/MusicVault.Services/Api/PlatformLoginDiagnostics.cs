using MusicVault.Services;

namespace MusicVault.Services.Api;

/// <summary>统一 API 平台登录状态诊断（Cookie 键与已登录源）。</summary>
public static class PlatformLoginDiagnostics
{
    public sealed record Result(bool Succeeded, string Message);

    public static async Task<Result> RunAsync(CancellationToken ct = default)
    {
        if (!GoMusicApiClient.IsConfigured)
            return new Result(false, ServiceMessageCodec.Key("Login.UnifiedApiNotConfigured"));

        try
        {
            var rawCookies = await GoMusicApiAccountClient.GetCookiesAsync(ct).ConfigureAwait(false);
            var normalized = MusicApiSessionHelper.NormalizePlatformCookies(rawCookies);
            var loggedIn = await MusicApiLoggedInSources.GetLoggedInSourceKeysAsync(ct).ConfigureAwait(false);

            var cookieKeys = normalized.Keys
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var loggedKeys = loggedIn
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new Result(
                true,
                ServiceMessageCodec.Format(
                    "Login.Diagnostic",
                    string.Join(", ", cookieKeys),
                    string.Join(", ", loggedKeys)));
        }
        catch (Exception ex)
        {
            return new Result(false, ServiceMessageCodec.Format("Login.DiagnosticFailed", ex.Message));
        }
    }
}
