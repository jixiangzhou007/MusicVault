using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MusicVault.Services.Http;
using MusicVault.Services;

namespace MusicVault.Services.Api;

public sealed record MusicRecognitionResult(
    bool Succeeded,
    string Message,
    string Keyword = "",
    string Artist = "",
    string Title = "",
    double? Confidence = null);

public static class MusicRecognitionClient
{
    private static readonly HttpClient Http = MusicSearchHttp.Client;

    public static async Task<MusicRecognitionResult> RecognizeAsync(
        string endpointUrl,
        string filePath,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(endpointUrl))
            return new MusicRecognitionResult(false, ServiceMessageCodec.Key("Recognition.EndpointNotConfigured"));
        if (!File.Exists(filePath))
            return new MusicRecognitionResult(false, ServiceMessageCodec.Key("Recognition.AudioNotFound"));

        var (postUrl, apiToken) = ParseEndpoint(endpointUrl.Trim());
        try
        {
            await using var stream = File.OpenRead(filePath);
            using var content = new MultipartFormDataContent();
            var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(fileContent, "file", Path.GetFileName(filePath));
            if (!string.IsNullOrWhiteSpace(apiToken))
                content.Add(new StringContent(apiToken), "api_token");

            using var response = await Http.PostAsync(postUrl, content, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return new MusicRecognitionResult(false, ServiceMessageCodec.Format("Recognition.EndpointResponse", (int)response.StatusCode));

            using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct).ConfigureAwait(false);
            if (doc == null)
                return new MusicRecognitionResult(false, ServiceMessageCodec.Key("Recognition.EndpointEmpty"));

            return ParseRecognitionDocument(doc.RootElement);
        }
        catch (Exception ex)
        {
            return new MusicRecognitionResult(false, RecognitionMessageMapper.MapException(ex));
        }
    }

    private static (string PostUrl, string? ApiToken) ParseEndpoint(string endpointUrl)
    {
        if (!Uri.TryCreate(endpointUrl, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Query))
            return (endpointUrl, null);

        string? token = null;
        foreach (var part in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2
                || !string.Equals(Uri.UnescapeDataString(kv[0]), "api_token", StringComparison.OrdinalIgnoreCase))
                continue;
            token = Uri.UnescapeDataString(kv[1]);
            break;
        }

        if (string.IsNullOrWhiteSpace(token))
            return (endpointUrl, null);

        var builder = new UriBuilder(uri) { Query = string.Empty };
        return (builder.Uri.ToString(), token);
    }

    private static MusicRecognitionResult ParseRecognitionDocument(JsonElement root)
    {
        if (TryParseAudD(root, out var audd))
            return audd;

        if (root.TryGetProperty("code", out var codeEl) && codeEl.ValueKind == JsonValueKind.Number && codeEl.GetInt32() != 200)
        {
            var msg = ReadString(root, "message", "msg", "error");
            return new MusicRecognitionResult(false, RecognitionMessageMapper.MapProviderMessage(msg));
        }

        if (root.TryGetProperty("status", out var statusEl)
            && statusEl.ValueKind == JsonValueKind.String
            && string.Equals(statusEl.GetString(), "error", StringComparison.OrdinalIgnoreCase))
        {
            var msg = ReadString(root, "error", "message", "msg");
            return new MusicRecognitionResult(false, RecognitionMessageMapper.MapProviderMessage(msg));
        }

        var payload = root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Object
            ? dataEl
            : root;

        return BuildResultFromPayload(payload);
    }

    private static bool TryParseAudD(JsonElement root, out MusicRecognitionResult result)
    {
        result = default!;
        if (!root.TryGetProperty("status", out var statusEl)
            || statusEl.ValueKind != JsonValueKind.String
            || !string.Equals(statusEl.GetString(), "success", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!root.TryGetProperty("result", out var resultEl) || resultEl.ValueKind != JsonValueKind.Object)
        {
            result = new MusicRecognitionResult(false, ServiceMessageCodec.Key("Recognition.NoMatch"));
            return true;
        }

        result = BuildResultFromPayload(resultEl);
        return true;
    }

    private static MusicRecognitionResult BuildResultFromPayload(JsonElement payload)
    {
        var artist = ReadString(payload, "artist", "singer", "author");
        var title = ReadString(payload, "title", "song", "name");
        var keyword = ReadString(payload, "keyword", "query", "text");
        var confidence = ReadDouble(payload, "confidence", "score");

        if (string.IsNullOrWhiteSpace(keyword))
        {
            keyword = !string.IsNullOrWhiteSpace(artist) && !string.IsNullOrWhiteSpace(title)
                ? $"{artist}-{title}"
                : !string.IsNullOrWhiteSpace(title)
                    ? title
                    : string.Empty;
        }

        if (string.IsNullOrWhiteSpace(keyword))
            return new MusicRecognitionResult(false, ServiceMessageCodec.Key("Recognition.NoKeyword"));

        return new MusicRecognitionResult(true, ServiceMessageCodec.Key("Recognition.Success"), keyword, artist, title, confidence);
    }

    private static string ReadString(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!element.TryGetProperty(key, out var value))
                continue;
            if (value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                    return text.Trim();
            }
        }

        return string.Empty;
    }

    private static double? ReadDouble(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!element.TryGetProperty(key, out var value))
                continue;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var num))
                return num;
            if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), out var parsed))
                return parsed;
        }

        return null;
    }
}
