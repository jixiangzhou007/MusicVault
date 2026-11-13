using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace MusicVault.App.Services;

/// <summary>从封面 Bitmap 采样主色，供 Hero 右侧色系延展。</summary>
public static class CoverAccentSampler
{
    private static readonly Color Fallback = Color.FromRgb(0x1A, 0x1A, 0x1E);

    public static Color Sample(Bitmap? bitmap)
    {
        if (bitmap == null)
            return Fallback;

        try
        {
            const int size = 24;
            using var scaled = bitmap.CreateScaledBitmap(new PixelSize(size, size), BitmapInterpolationMode.LowQuality);
            var pixels = new byte[size * size * 4];
            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                scaled.CopyPixels(
                    new PixelRect(0, 0, size, size),
                    handle.AddrOfPinnedObject(),
                    pixels.Length,
                    size * 4);
            }
            finally
            {
                handle.Free();
            }

            long rSum = 0, gSum = 0, bSum = 0, n = 0;
            for (var i = 0; i < pixels.Length; i += 4)
            {
                var b = pixels[i];
                var g = pixels[i + 1];
                var r = pixels[i + 2];
                var a = pixels[i + 3];
                if (a < 16)
                    continue;
                rSum += r;
                gSum += g;
                bSum += b;
                n++;
            }

            if (n == 0)
                return Fallback;

            return ToneForPanel(Color.FromRgb(
                (byte)(rSum / n),
                (byte)(gSum / n),
                (byte)(bSum / n)));
        }
        catch
        {
            return Fallback;
        }
    }

    /// <summary>压暗主色，保证白字可读。</summary>
    public static Color ToneForPanel(Color c)
    {
        var r = c.R / 255.0;
        var g = c.G / 255.0;
        var b = c.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var lightness = (max + min) / 2.0;
        var target = 0.32;
        var scale = lightness < 0.05 ? 1.0 : target / lightness;
        scale = Math.Clamp(scale, 0.35, 2.2);
        return Color.FromRgb(
            (byte)Math.Clamp((int)(c.R * scale), 18, 90),
            (byte)Math.Clamp((int)(c.G * scale), 18, 90),
            (byte)Math.Clamp((int)(c.B * scale), 18, 90));
    }

    public static IBrush CreateBrush(Color color) => new SolidColorBrush(color);
}
