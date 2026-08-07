using Avalonia;
using Avalonia.Media.Imaging;

namespace MusicVault.App.Services;

/// <summary>封面缩略图生成（用于底部栏等小尺寸场景）。</summary>
public static class CoverImageScaler
{
    public static byte[]? ScaleToMaxEdge(byte[] source, int maxEdge)
    {
        if (source.Length == 0 || maxEdge <= 0)
            return source;

        try
        {
            using var input = new MemoryStream(source);
            var bitmap = new Bitmap(input);
            if (bitmap.PixelSize.Width <= maxEdge && bitmap.PixelSize.Height <= maxEdge)
                return source;

            var scale = Math.Min(
                (double)maxEdge / bitmap.PixelSize.Width,
                (double)maxEdge / bitmap.PixelSize.Height);
            var w = Math.Max(1, (int)Math.Round(bitmap.PixelSize.Width * scale));
            var h = Math.Max(1, (int)Math.Round(bitmap.PixelSize.Height * scale));
            using var scaled = bitmap.CreateScaledBitmap(
                new PixelSize(w, h),
                BitmapInterpolationMode.HighQuality);
            using var output = new MemoryStream();
            scaled.Save(output);
            return output.ToArray();
        }
        catch
        {
            return source;
        }
    }
}
