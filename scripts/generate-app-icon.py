"""Generate MusicVault app-icon.ico / .png from the vinyl brand mark."""
from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
SIZES = [16, 24, 32, 48, 64, 128, 256]


def make_icon(size: int) -> Image.Image:
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    scale = size / 512.0

    def r(v: float) -> int:
        return int(round(v * scale))

    cx = cy = size // 2
    pad = r(16)
    d.ellipse(
        [pad, pad, size - 1 - pad, size - 1 - pad],
        fill=(26, 26, 46, 255),
        outline=(0, 122, 255, 255),
        width=max(1, r(8)),
    )
    for rad, w in [(200, 2), (180, 2), (160, 1)]:
        rr = r(rad)
        d.ellipse(
            [cx - rr, cy - rr, cx + rr, cy + rr],
            outline=(45, 45, 68, 255),
            width=max(1, r(w)),
        )
    lr = r(80)
    d.ellipse([cx - lr, cy - lr, cx + lr, cy + lr], fill=(0, 122, 255, 255))

    font_size = max(10, r(96))
    font = None
    for name in (
        r"C:\Windows\Fonts\segoeuib.ttf",
        r"C:\Windows\Fonts\arialbd.ttf",
        r"C:\Windows\Fonts\msyhbd.ttc",
    ):
        try:
            font = ImageFont.truetype(name, font_size)
            break
        except OSError:
            pass
    if font is None:
        font = ImageFont.load_default()

    text = "V"
    bbox = d.textbbox((0, 0), text, font=font)
    tw, th = bbox[2] - bbox[0], bbox[3] - bbox[1]
    tx = cx - tw // 2 - bbox[0]
    ty = cy - th // 2 - bbox[1] - max(1, r(6))
    d.text((tx, ty), text, font=font, fill=(255, 255, 255, 255))

    sr = max(1, r(12))
    d.ellipse([cx - sr, cy - sr, cx + sr, cy + sr], fill=(26, 26, 46, 255))
    return img


def main() -> None:
    assets = ROOT / "assets"
    app_assets = ROOT / "src" / "MusicVault.App" / "Assets"
    assets.mkdir(parents=True, exist_ok=True)
    app_assets.mkdir(parents=True, exist_ok=True)

    base = make_icon(256)
    ico_path = assets / "app-icon.ico"
    png_path = assets / "app-icon.png"
    # Pillow 按 sizes 从大图缩放写入多尺寸 ICO（任务栏/资源管理器需要）
    base.save(ico_path, format="ICO", sizes=[(s, s) for s in SIZES])
    base.save(png_path, format="PNG")
    (app_assets / "app-icon.ico").write_bytes(ico_path.read_bytes())
    (app_assets / "app-icon.png").write_bytes(png_path.read_bytes())
    print(f"wrote {ico_path} ({ico_path.stat().st_size} bytes)")
    print(f"wrote {app_assets / 'app-icon.ico'}")
    print(f"wrote {png_path}")


if __name__ == "__main__":
    main()
