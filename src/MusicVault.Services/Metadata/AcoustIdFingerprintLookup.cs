using System.Net.Http.Json;
using System.Text.Json;

namespace MusicVault.Services.Metadata;

public enum AcoustIdLookupStatus
{
    Disabled,
    FileMissing,
    NoKey,
    NoFpcalc,
    FpcalcFailed,
    HttpError,
    ApiError,
    NoMatch,
    Success
}

public sealed record AcoustIdLookupResult(AcoustIdLookupStatus Status, string? AcoustId = null);

/// <summary>
/// 通过 fpcalc（Chromaprint CLI）+ AcoustID API 在线 lookup 指纹。
/// 需在设置中启用并配置 API Key（或环境变量 <c>MUSICVAULT_ACOUSTID_API_KEY</c>），且 PATH 中可用 fpcalc。
/// </summary>
public static class AcoustIdFingerprintLookup
{
    private static readonly HttpClient Http = MusicVault.Services.Http.HttpClients.Default;
    private const string LookupUrl = "https://api.acoustid.org/v2/lookup";

    public static bool IsConfigured =>
        AcoustIdLookupConfiguration.Enabled && !string.IsNullOrWhiteSpace(ResolveApiKey());

    public static bool IsFpcalcAvailable => ResolveFpcalcPath() != null;

    public static async Task<string?> LookupAsync(string filePath, CancellationToken ct = default)
    {
        var result = await LookupDetailedAsync(filePath, ct).ConfigureAwait(false);
        return result.Status == AcoustIdLookupStatus.Success ? result.AcoustId : null;
    }

    public static async Task<AcoustIdLookupResult> LookupDetailedAsync(
        string filePath,
        CancellationToken ct = default)
    {
        if (!AcoustIdLookupConfiguration.Enabled)
            return new(AcoustIdLookupStatus.Disabled);

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return new(AcoustIdLookupStatus.FileMissing);

        var apiKey = ResolveApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
            return new(AcoustIdLookupStatus.NoKey);

        var fpcalc = ResolveFpcalcPath();
        if (fpcalc == null)
            return new(AcoustIdLookupStatus.NoFpcalc);

        var (durationSec, fingerprint) = await RunFpcalcAsync(fpcalc, filePath, ct).ConfigureAwait(false);
        if (durationSec <= 0 || string.IsNullOrWhiteSpace(fingerprint))
            return new(AcoustIdLookupStatus.FpcalcFailed);

        return await QueryAcoustIdDetailedAsync(apiKey, fingerprint, durationSec, ct).ConfigureAwait(false);
    }

    internal static async Task<(int DurationSec, string? Fingerprint)> RunFpcalcAsync(
        string fpcalcPath,
        string filePath,
        CancellationToken ct)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = fpcalcPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        psi.ArgumentList.Add("-json");
        psi.ArgumentList.Add(filePath);

        using var process = System.Diagnostics.Process.Start(psi);
        if (process == null)
            return (0, null);

        var output = await process.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        if (process.ExitCode != 0)
            return (0, null);

        return ParseFpcalcJson(output);
    }

    internal static (int DurationSec, string? Fingerprint) ParseFpcalcJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return (0, null);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var duration = root.TryGetProperty("duration", out var dEl) && dEl.TryGetDouble(out var d)
                ? (int)Math.Round(d)
                : 0;
            var fingerprint = root.TryGetProperty("fingerprint", out var fpEl)
                ? fpEl.GetString()
                : null;
            return duration > 0 && !string.IsNullOrWhiteSpace(fingerprint)
                ? (duration, fingerprint)
                : (0, null);
        }
        catch
        {
            return (0, null);
        }
    }

    internal static async Task<string?> QueryAcoustIdAsync(
        string apiKey,
        string fingerprint,
        int durationSec,
        CancellationToken ct)
    {
        var detailed = await QueryAcoustIdDetailedAsync(apiKey, fingerprint, durationSec, ct).ConfigureAwait(false);
        return detailed.Status == AcoustIdLookupStatus.Success ? detailed.AcoustId : null;
    }

    internal static async Task<AcoustIdLookupResult> QueryAcoustIdDetailedAsync(
        string apiKey,
        string fingerprint,
        int durationSec,
        CancellationToken ct)
    {
        var url =
            $"{LookupUrl}?client={Uri.EscapeDataString(apiKey)}&meta=recordings&format=json";
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["fingerprint"] = fingerprint,
            ["duration"] = durationSec.ToString()
        });

        using var resp = await Http.PostAsync(url, content, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            return new(AcoustIdLookupStatus.HttpError);

        using var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct).ConfigureAwait(false);
        if (doc == null)
            return new(AcoustIdLookupStatus.HttpError);

        if (doc.RootElement.TryGetProperty("status", out var statusEl)
            && statusEl.GetString() != "ok")
            return new(AcoustIdLookupStatus.ApiError);

        if (!doc.RootElement.TryGetProperty("results", out var results)
            || results.ValueKind != JsonValueKind.Array)
            return new(AcoustIdLookupStatus.NoMatch);

        foreach (var item in results.EnumerateArray())
        {
            if (!item.TryGetProperty("id", out var idEl))
                continue;

            var id = idEl.GetString()?.Trim();
            if (IsValidAcoustId(id))
                return new(AcoustIdLookupStatus.Success, id);
        }

        return new(AcoustIdLookupStatus.NoMatch);
    }

    private static string? ResolveApiKey() => AcoustIdLookupConfiguration.ResolveApiKey();

    private static string? ResolveFpcalcPath()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathEnv))
            return null;

        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var name in new[] { "fpcalc.exe", "fpcalc" })
            {
                var candidate = Path.Combine(dir.Trim(), name);
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }

    private static bool IsValidAcoustId(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length >= 32
        && value.All(c => char.IsLetterOrDigit(c) || c == '-');
}
