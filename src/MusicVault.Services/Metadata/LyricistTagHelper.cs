using TagLib;
using TagLib.Id3v2;
using TagLib.Ogg;

namespace MusicVault.Services.Metadata;

internal static class LyricistTagHelper
{
    private const string XiphField = "LYRICIST";
    private const string UserTextDescription = "LYRICIST";

    public static string Read(TagLib.File file)
    {
        if (file.GetTag(TagTypes.Id3v2) is TagLib.Id3v2.Tag id3)
        {
            var textFrame = TextInformationFrame.Get(id3, "TEXT", false);
            if (textFrame?.Text is { Length: > 0 } text && !string.IsNullOrWhiteSpace(text[0]))
                return text[0].Trim();

            var userFrame = UserTextInformationFrame.Get(id3, UserTextDescription, false);
            if (userFrame?.Text is { Length: > 0 } userText && !string.IsNullOrWhiteSpace(userText[0]))
                return userText[0].Trim();
        }

        if (file.GetTag(TagTypes.Xiph) is XiphComment xiph)
        {
            var fields = xiph.GetField(XiphField);
            if (fields is { Length: > 0 } && !string.IsNullOrWhiteSpace(fields[0]))
                return fields[0].Trim();
        }

        return string.Empty;
    }

    public static void Write(TagLib.File file, string lyricist)
    {
        var value = lyricist.Trim();
        if (file.GetTag(TagTypes.Id3v2, true) is TagLib.Id3v2.Tag id3)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                var textFrame = TextInformationFrame.Get(id3, "TEXT", false);
                if (textFrame != null)
                    id3.RemoveFrame(textFrame);

                var userFrame = UserTextInformationFrame.Get(id3, UserTextDescription, false);
                if (userFrame != null)
                    id3.RemoveFrame(userFrame);
            }
            else
            {
                var textFrame = TextInformationFrame.Get(id3, "TEXT", true);
                textFrame.Text = [value];

                var userFrame = UserTextInformationFrame.Get(id3, UserTextDescription, true);
                userFrame.Text = [value];
            }
        }

        if (file.GetTag(TagTypes.Xiph, true) is XiphComment xiph)
        {
            if (string.IsNullOrWhiteSpace(value))
                xiph.RemoveField(XiphField);
            else
                xiph.SetField(XiphField, [value]);
        }
    }
}
