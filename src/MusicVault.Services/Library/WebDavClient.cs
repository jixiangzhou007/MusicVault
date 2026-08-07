using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using MusicVault.Core.Audio;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Core.Nas;
using MusicVault.Services;

namespace MusicVault.Services.Library;

public sealed record WebDavRemoteEntry(string Url, long SizeBytes, bool IsCollection);

/// <summary>WebDAV PROPFIND / GET 客户端（NAS N5）。</summary>
public sealed class WebDavClient
{
    private const string PropfindBody = """
        <?xml version="1.0" encoding="utf-8" ?>
        <D:propfind xmlns:D="DAV:">
          <D:prop>
            <D:resourcetype/>
            <D:getcontentlength/>
          </D:prop>
        </D:propfind>
        """;

    private static readonly XNamespace Dav = "DAV:";

    public async Task<NasConnectionTestResult> TestAsync(NetworkLibraryEndpoint endpoint, CancellationToken ct = default)
    {
        var root = endpoint.BuildWebDavRootPath();
        if (string.IsNullOrWhiteSpace(root))
            return NasConnectionTestResult.Fail(ServiceMessageCodec.Key("WebDav.HostRequired"));

        try
        {
            using var http = CreateHttpClient(endpoint);
            using var request = BuildPropfindRequest(root, depth: 0);
            using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
                return NasConnectionTestResult.Ok(root, ServiceMessageCodec.Key("WebDav.PathAccessible"));

            return NasConnectionTestResult.Fail(
                ServiceMessageCodec.Format("WebDav.ConnectFailedDetail", $"{(int)response.StatusCode} {response.ReasonPhrase}"));
        }
        catch (Exception ex)
        {
            return NasConnectionTestResult.Fail(ServiceMessageCodec.Format("WebDav.TestFailed", ex.Message));
        }
    }

    public async Task<IReadOnlyList<WebDavRemoteEntry>> ListAudioFilesAsync(
        NetworkLibraryEndpoint endpoint,
        IEnumerable<string> extensions,
        CancellationToken ct = default)
    {
        var root = WebDavPathHelper.NormalizeDirectoryUrl(endpoint.BuildWebDavRootPath());
        if (string.IsNullOrWhiteSpace(root))
            return [];

        var extSet = new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase);
        var results = new List<WebDavRemoteEntry>();
        var queue = new Queue<string>();
        queue.Enqueue(root);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { root };

        using var http = CreateHttpClient(endpoint);

        while (queue.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var folder = queue.Dequeue();
            var entries = await PropfindEntriesAsync(http, folder, ct).ConfigureAwait(false);
            foreach (var entry in entries)
            {
                if (entry.IsCollection)
                {
                    if (visited.Add(entry.Url))
                        queue.Enqueue(entry.Url);
                    continue;
                }

                var ext = Path.GetExtension(WebDavPathHelper.GetFileName(entry.Url));
                if (extSet.Contains(ext))
                    results.Add(entry);
            }
        }

        return results;
    }

    public async Task CopyToFileAsync(
        NetworkLibraryEndpoint endpoint,
        string fileUrl,
        string destinationPath,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        var temp = destinationPath + ".part";
        await using (var dst = File.Create(temp))
        await using (var stream = await OpenReadAsync(endpoint, fileUrl, ct).ConfigureAwait(false))
            await stream.CopyToAsync(dst, ct).ConfigureAwait(false);

        if (File.Exists(destinationPath))
            File.Delete(destinationPath);
        File.Move(temp, destinationPath);
    }

    public async Task<Stream> OpenReadAsync(
        NetworkLibraryEndpoint endpoint,
        string fileUrl,
        CancellationToken ct = default)
    {
        var http = CreateHttpClient(endpoint);
        var response = await http.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
    }

    public async Task<byte[]> ReadRangeAsync(
        NetworkLibraryEndpoint endpoint,
        string fileUrl,
        long start,
        int length,
        CancellationToken ct = default)
    {
        if (length <= 0)
            return [];

        using var http = CreateHttpClient(endpoint);
        using var request = new HttpRequestMessage(HttpMethod.Get, fileUrl);
        request.Headers.Range = new RangeHeaderValue(start, start + length - 1);

        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);

        if (response.StatusCode is not (HttpStatusCode.PartialContent or HttpStatusCode.OK))
            response.EnsureSuccessStatusCode();

        var data = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        if (data.Length <= length)
            return data;

        return data.AsSpan(0, length).ToArray();
    }

    internal static HttpClient CreateHttpClient(NetworkLibraryEndpoint endpoint, HttpMessageHandler? handler = null)
    {
        var http = handler == null ? new HttpClient() : new HttpClient(handler);
        http.Timeout = TimeSpan.FromSeconds(120);
        ApplyAuth(http, endpoint);
        return http;
    }

    internal static void ApplyAuth(HttpClient http, NetworkLibraryEndpoint endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint.Username))
            return;

        var token = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{endpoint.Username}:{endpoint.Password}"));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
    }

    private static HttpRequestMessage BuildPropfindRequest(string url, int depth)
    {
        var request = new HttpRequestMessage(new HttpMethod("PROPFIND"), url)
        {
            Content = new StringContent(PropfindBody, Encoding.UTF8, "application/xml")
        };
        request.Headers.TryAddWithoutValidation("Depth", depth.ToString());
        return request;
    }

    private static async Task<IReadOnlyList<WebDavRemoteEntry>> PropfindEntriesAsync(
        HttpClient http,
        string folderUrl,
        CancellationToken ct)
    {
        using var request = BuildPropfindRequest(WebDavPathHelper.NormalizeDirectoryUrl(folderUrl), depth: 1);
        using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var xml = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return ParsePropfindResponse(folderUrl, xml);
    }

    internal static IReadOnlyList<WebDavRemoteEntry> ParsePropfindResponse(string folderUrl, string xml)
    {
        var list = new List<WebDavRemoteEntry>();
        if (string.IsNullOrWhiteSpace(xml))
            return list;

        var doc = XDocument.Parse(xml);
        var normalizedFolder = WebDavPathHelper.NormalizeDirectoryUrl(folderUrl);

        foreach (var response in doc.Descendants(Dav + "response"))
        {
            var href = response.Element(Dav + "href")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(href))
                continue;

            var absolute = ToAbsoluteUrl(normalizedFolder, href);
            if (string.Equals(
                    WebDavPathHelper.NormalizeDirectoryUrl(absolute),
                    normalizedFolder,
                    StringComparison.OrdinalIgnoreCase))
                continue;

            var prop = response.Descendants(Dav + "propstat")
                .FirstOrDefault(ps => (ps.Element(Dav + "status")?.Value ?? string.Empty)
                    .Contains("200", StringComparison.Ordinal));

            prop ??= response.Element(Dav + "propstat")?.Element(Dav + "prop")?.Parent;

            var propNode = prop?.Element(Dav + "prop");
            var isCollection = propNode?.Element(Dav + "resourcetype")?.Element(Dav + "collection") != null;
            long size = 0;
            var sizeText = propNode?.Element(Dav + "getcontentlength")?.Value;
            if (!string.IsNullOrWhiteSpace(sizeText))
                long.TryParse(sizeText.Trim(), out size);

            list.Add(new WebDavRemoteEntry(absolute, size, isCollection));
        }

        return list;
    }

    private static string ToAbsoluteUrl(string folderUrl, string href)
    {
        if (Uri.TryCreate(href, UriKind.Absolute, out var absolute))
            return absolute.ToString();

        var baseUri = new Uri(WebDavPathHelper.NormalizeDirectoryUrl(folderUrl));
        return new Uri(baseUri, href).ToString();
    }
}
