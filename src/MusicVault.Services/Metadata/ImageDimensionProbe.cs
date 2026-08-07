namespace MusicVault.Services.Metadata;

internal static class ImageDimensionProbe
{
    public static (int Width, int Height)? TryReadJpegDimensions(byte[] data)
    {
        if (data.Length < 12 || data[0] != 0xFF || data[1] != 0xD8)
            return null;

        var i = 2;
        while (i + 9 < data.Length)
        {
            if (data[i] != 0xFF)
            {
                i++;
                continue;
            }

            var marker = data[i + 1];
            if (marker is 0xC0 or 0xC1 or 0xC2 or 0xC3 or 0xC5 or 0xC6 or 0xC7 or 0xC9 or 0xCA or 0xCB or 0xCD or 0xCE or 0xCF)
            {
                var height = (data[i + 5] << 8) | data[i + 6];
                var width = (data[i + 7] << 8) | data[i + 8];
                return width > 0 && height > 0 ? (width, height) : null;
            }

            var segmentLength = (data[i + 2] << 8) | data[i + 3];
            if (segmentLength < 2)
                return null;

            i += 2 + segmentLength;
        }

        return null;
    }
}
