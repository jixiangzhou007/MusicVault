using System.Text;

namespace MusicVault.App.Metadata;

public static class MetadataEncodingFixHelper
{
    private static readonly (string Name, string CodePage)[] Encodings =
    [
        ("UTF-8", "utf-8"),
        ("GB18030", "GB18030"),
        ("GBK", "GBK"),
        ("Big5", "big5"),
        ("ISO-8859-1", "ISO-8859-1"),
        ("UTF-16 LE", "utf-16"),
        ("UTF-16 BE", "unicodeFFFE")
    ];

    public static IReadOnlyList<EncodingFixCandidate> GetCandidates(string garbled)
    {
        if (string.IsNullOrEmpty(garbled))
            return [];

        var list = new List<EncodingFixCandidate>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (name, codePage) in Encodings)
        {
            var preview = TryDecode(garbled, codePage);
            if (string.IsNullOrEmpty(preview) || !seen.Add(preview))
                continue;

            list.Add(new EncodingFixCandidate(name, preview));
        }

        return list;
    }

    public static string TryDecode(string text, string codePage)
    {
        try
        {
            var latin1 = Encoding.GetEncoding("ISO-8859-1");
            var bytes = latin1.GetBytes(text);
            return Encoding.GetEncoding(codePage).GetString(bytes);
        }
        catch
        {
            return string.Empty;
        }
    }
}

public sealed record EncodingFixCandidate(string EncodingName, string Preview);
