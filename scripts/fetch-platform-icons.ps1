# 从 App Store（中国区 iTunes Search）拉取主流音乐平台原厂应用图标。
# 用法: .\scripts\fetch-platform-icons.ps1
$ErrorActionPreference = "Stop"
$outDir = Join-Path $PSScriptRoot "..\src\MusicVault.App\Assets\Platforms"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$apps = @(
    @{ Key = "qq";      Term = "QQ音乐";     Prefer = "QQ音乐 - 听我想听" },
    @{ Key = "netease"; Term = "网易云音乐"; Prefer = "网易云音乐" },
    @{ Key = "kugou";   Term = "酷狗音乐";   Prefer = "酷狗音乐 - 就是歌多" },
    @{ Key = "kuwo";    Term = "酷我音乐";   Prefer = "酷我音乐" },
    @{ Key = "migu";    Term = "咪咕音乐";   Prefer = "咪咕音乐" },
    @{ Key = "soda";    Term = "汽水音乐";   Prefer = "汽水音乐" }
)

python -c @"
from pathlib import Path
import json, urllib.parse, urllib.request
from io import BytesIO
from PIL import Image

out = Path(r'$($outDir -replace '\\','\\')')
apps = $(ConvertTo-Json $apps -Compress)

opener = urllib.request.build_opener()
opener.addheaders = [('User-Agent', 'Mozilla/5.0 MusicVaultIconFetch/1.0')]

def lookup(term, prefer):
    q = urllib.parse.urlencode({'term': term, 'country': 'cn', 'entity': 'software', 'limit': 5})
    url = f'https://itunes.apple.com/search?{q}'
    with opener.open(url, timeout=30) as resp:
        data = json.load(resp)
    results = data.get('results') or []
    if prefer:
        for r in results:
            if prefer in (r.get('trackName') or ''):
                return r
    return results[0] if results else None

for a in apps:
    hit = lookup(a['Term'], a.get('Prefer'))
    if not hit:
        raise SystemExit(f\"no hit for {a['Key']}\")
    art = hit.get('artworkUrl512') or hit.get('artworkUrl100')
    if not art:
        raise SystemExit(f\"no artwork for {a['Key']}\")
    hi = art.replace('/512x512bb.jpg', '/1024x1024bb.jpg')
    for cand in (hi, art):
        try:
            with opener.open(cand, timeout=30) as resp:
                raw = resp.read()
            break
        except Exception:
            raw = None
    if not raw:
        raise SystemExit(f\"download failed {a['Key']}\")
    img = Image.open(BytesIO(raw)).convert('RGBA').resize((256, 256), Image.Resampling.LANCZOS)
    dest = out / f\"{a['Key']}.png\"
    img.save(dest, 'PNG')
    print(f\"ok {a['Key']} <- {hit.get('trackName')} ({hit.get('sellerName')})\")
print('done')
"@
