using TagLib;
using TagLib.Id3v2;
using TagLib.Ogg;

namespace MusicVault.Services.Metadata;

/// <summary>从音频标签读取 AcoustID（扫描入库，用于库内相似曲匹配）。</summary>
public static class AcoustIdTagReader
{
    private static readonly string[] XiphFields = ["ACOUSTID_ID", "ACOUSTID ID"];
    private static readonly string[] UserTextDescriptions = ["Acoustid Id", "ACOUSTID_ID", "AcoustID Id"];

    public static string? Read(TagLib.File file)
    {
        foreach (var name in XiphFields)
        {
            var value = ReadXiphField(file, name);
            if (IsValidId(value))
                return value;
        }

        if (file.GetTag(TagTypes.Id3v2) is TagLib.Id3v2.Tag id3)
        {
            foreach (var description in UserTextDescriptions)
            {
                var frame = UserTextInformationFrame.Get(id3, description, false);
                var text = frame?.Text?.FirstOrDefault()?.Trim();
                if (IsValidId(text))
                    return text;
            }
        }

        return null;
    }

    private static string? ReadXiphField(TagLib.File file, string fieldName)
    {
        if (file.GetTag(TagTypes.Xiph) is not XiphComment xiph)
            return null;

        var fields = xiph.GetField(fieldName);
        return fields is { Length: > 0 } ? fields[0]?.Trim() : null;
    }

    private static bool IsValidId(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length >= 32
        && value.All(c => char.IsLetterOrDigit(c) || c == '-');
}
