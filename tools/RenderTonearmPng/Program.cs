using SkiaSharp;
using Svg.Skia;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var svgPath = Path.Combine(root, "src", "MusicVault.App", "Assets", "vinyl-tonearm.svg");
var pngPath = Path.Combine(root, "src", "MusicVault.App", "Assets", "vinyl-tonearm.png");

using var svg = new SKSvg();
if (svg.Load(svgPath) is null)
{
    Console.Error.WriteLine($"Failed to load SVG: {svgPath}");
    return 1;
}

const int width = 368;
const int height = 552;
using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
var canvas = surface.Canvas;
canvas.Clear(SKColors.Transparent);

var bounds = svg.Picture.CullRect;
var scale = Math.Min(width / bounds.Width, height / bounds.Height) * 0.92f;
var dx = (width - bounds.Width * scale) / 2f - bounds.Left * scale;
var dy = (height - bounds.Height * scale) / 2f - bounds.Top * scale;
canvas.Save();
canvas.Translate(dx, dy);
canvas.Scale(scale);
canvas.DrawPicture(svg.Picture);
canvas.Restore();

using var image = surface.Snapshot();
using var data = image.Encode(SKEncodedImageFormat.Png, 100);
await using var stream = File.OpenWrite(pngPath);
data.SaveTo(stream);
Console.WriteLine($"Wrote {pngPath}");
return 0;
