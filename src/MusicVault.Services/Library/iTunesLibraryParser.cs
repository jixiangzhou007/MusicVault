using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using MusicVault.Services;

namespace MusicVault.Services.Library;

/// <summary>
/// iTunes "Library.xml" 解析器。
/// 支持 iTunes 导出的 PLIST XML 格式，提取曲目元数据和播放列表。
/// </summary>
public static class iTunesLibraryParser
{
    /// <summary>解析结果</summary>
    public sealed record iTunesLibrary
    {
        public string MusicFolder { get; init; } = string.Empty;
        public IReadOnlyList<iTunesTrack> Tracks { get; init; } = [];
        public IReadOnlyList<iTunesPlaylist> Playlists { get; init; } = [];
    }

    public sealed record iTunesTrack
    {
        public int TrackId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Artist { get; init; } = string.Empty;
        public string Album { get; init; } = string.Empty;
        public string AlbumArtist { get; init; } = string.Empty;
        public string Genre { get; init; } = string.Empty;
        public int? Year { get; init; }
        public int? TrackNumber { get; init; }
        public int? TrackCount { get; init; }
        public int? DiscNumber { get; init; }
        public int? DiscCount { get; init; }
        public int? TotalTime { get; init; } // milliseconds
        public long? Size { get; init; }
        public string Location { get; init; } = string.Empty;
        public string PersistentId { get; init; } = string.Empty;

        /// <summary>将 file:// URL 解码为本地绝对路径。</summary>
        public string? GetLocalPath()
        {
            if (string.IsNullOrWhiteSpace(Location))
                return null;

            var path = Location;
            // file://localhost/C:/... or file:///C:/...
            if (path.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                path = path[7..]; // Remove "file://"
                if (path.StartsWith("localhost/", StringComparison.OrdinalIgnoreCase))
                    path = path[10..]; // Remove "localhost/"
            }

            path = Uri.UnescapeDataString(path);
            // 规范化路径分隔符
            path = path.Replace('/', Path.DirectorySeparatorChar);

            return File.Exists(path) ? Path.GetFullPath(path) : null;
        }
    }

    public sealed record iTunesPlaylist
    {
        public string Name { get; init; } = string.Empty;
        public bool IsFolder { get; init; }
        public bool IsSmart { get; init; }
        public IReadOnlyList<int> TrackIds { get; init; } = [];
    }

    /// <summary>解析 iTunes Library.xml 文件。</summary>
    public static iTunesLibrary Parse(string xmlPath)
    {
        if (!File.Exists(xmlPath))
            throw new FileNotFoundException(ServiceMessageCodec.Key("Library.iTunes.FileNotFound"), xmlPath);

        var doc = XDocument.Load(xmlPath);
        var root = doc.Root;
        if (root?.Name != "plist")
            throw new FormatException(ServiceMessageCodec.Key("Library.iTunes.InvalidRoot"));

        var topDict = root.Element("dict");
        if (topDict == null)
            throw new FormatException(ServiceMessageCodec.Key("Library.iTunes.NoDict"));

        var topDictChildren = topDict.Elements().ToList();

        // 解析 Music Folder
        var musicFolder = ReadStringValue(topDictChildren, "Music Folder");

        // 解析 Tracks
        var tracks = new List<iTunesTrack>();
        var tracksDict = GetDictForKey(topDictChildren, "Tracks");
        if (tracksDict != null)
        {
            var trackEntries = ParseKeyDictPairs(tracksDict);
            foreach (var (key, trackDict) in trackEntries)
            {
                if (!int.TryParse(key, out var trackId))
                    continue;
                tracks.Add(ParseTrack(trackId, trackDict));
            }
        }

        // 解析 Playlists
        var playlists = new List<iTunesPlaylist>();
        var playlistsArray = GetArrayForKey(topDictChildren, "Playlists");
        if (playlistsArray != null)
        {
            foreach (var playlistDict in playlistsArray.Elements("dict"))
            {
                playlists.Add(ParsePlaylist(playlistDict));
            }
        }

        return new iTunesLibrary
        {
            MusicFolder = musicFolder,
            Tracks = tracks,
            Playlists = playlists
        };
    }

    /// <summary>扫描库中有本地文件的曲目，返回有效文件路径列表。</summary>
    public static IReadOnlyList<string> GetAvailableTrackPaths(iTunesLibrary library)
    {
        var result = new List<string>();
        foreach (var track in library.Tracks)
        {
            var localPath = track.GetLocalPath();
            if (localPath != null)
                result.Add(localPath);
        }
        return result;
    }

    private static iTunesTrack ParseTrack(int trackId, XElement dictElement)
    {
        var dict = dictElement.Elements().ToList();
        var track = new iTunesTrack { TrackId = trackId };

        foreach (var pair in EnumerateKeyValuePairs(dict))
        {
            switch (pair.Key)
            {
                case "Name": track = track with { Name = pair.Value }; break;
                case "Artist": track = track with { Artist = pair.Value }; break;
                case "Album": track = track with { Album = pair.Value }; break;
                case "Album Artist": track = track with { AlbumArtist = pair.Value }; break;
                case "Genre": track = track with { Genre = pair.Value }; break;
                case "Year": track = track with { Year = ParseInt(pair.Value) }; break;
                case "Track Number": track = track with { TrackNumber = ParseInt(pair.Value) }; break;
                case "Track Count": track = track with { TrackCount = ParseInt(pair.Value) }; break;
                case "Disc Number": track = track with { DiscNumber = ParseInt(pair.Value) }; break;
                case "Disc Count": track = track with { DiscCount = ParseInt(pair.Value) }; break;
                case "Total Time": track = track with { TotalTime = ParseInt(pair.Value) }; break;
                case "Size": track = track with { Size = ParseLong(pair.Value) }; break;
                case "Location": track = track with { Location = pair.Value }; break;
                case "Persistent ID": track = track with { PersistentId = pair.Value }; break;
            }
        }

        return track;
    }

    private static iTunesPlaylist ParsePlaylist(XElement dictElement)
    {
        var dict = dictElement.Elements().ToList();
        var playlist = new iTunesPlaylist();
        var trackIds = new List<int>();

        foreach (var pair in EnumerateKeyValuePairs(dict))
        {
            switch (pair.Key)
            {
                case "Name": playlist = playlist with { Name = pair.Value }; break;
                case "Folder": playlist = playlist with { IsFolder = pair.Value == "true" }; break;
                case "Smart Info": playlist = playlist with { IsSmart = true }; break;
                case "Playlist Items":
                    var itemsArray = dict.FirstOrDefault(e => e.Name == "array");
                    if (itemsArray != null)
                    {
                        foreach (var item in itemsArray.Elements("dict"))
                        {
                            var itemDict = item.Elements().ToList();
                            var trackIdStr = ReadStringValue(itemDict, "Track ID");
                            if (int.TryParse(trackIdStr, out var tid))
                                trackIds.Add(tid);
                        }
                    }
                    break;
            }
        }

        return playlist with { TrackIds = trackIds };
    }

    // ── PLIST XML helpers ──

    private static IEnumerable<(string Key, string Value)> EnumerateKeyValuePairs(List<XElement> elements)
    {
        for (var i = 0; i < elements.Count - 1; i++)
        {
            if (elements[i].Name == "key" && elements[i + 1].Name != "key")
            {
                var key = elements[i].Value;
                var value = elements[i + 1].Value;
                yield return (key, value);
            }
        }
    }

    private static string ReadStringValue(List<XElement> elements, string key)
    {
        for (var i = 0; i < elements.Count - 1; i++)
        {
            if (elements[i].Name == "key" && elements[i].Value == key)
            {
                var next = elements[i + 1];
                return next.Name == "string" ? next.Value : string.Empty;
            }
        }
        return string.Empty;
    }

    private static XElement? GetDictForKey(List<XElement> elements, string key)
    {
        for (var i = 0; i < elements.Count - 1; i++)
        {
            if (elements[i].Name == "key" && elements[i].Value == key && elements[i + 1].Name == "dict")
                return elements[i + 1];
        }
        return null;
    }

    private static XElement? GetArrayForKey(List<XElement> elements, string key)
    {
        for (var i = 0; i < elements.Count - 1; i++)
        {
            if (elements[i].Name == "key" && elements[i].Value == key && elements[i + 1].Name == "array")
                return elements[i + 1];
        }
        return null;
    }

    private static List<(string Key, XElement Value)> ParseKeyDictPairs(XElement dict)
    {
        var result = new List<(string, XElement)>();
        var elements = dict.Elements().ToList();
        for (var i = 0; i < elements.Count - 1; i++)
        {
            if (elements[i].Name == "key")
            {
                var next = elements[i + 1];
                if (next.Name == "dict")
                    result.Add((elements[i].Value, next));
            }
        }
        return result;
    }

    private static int? ParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static long? ParseLong(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
