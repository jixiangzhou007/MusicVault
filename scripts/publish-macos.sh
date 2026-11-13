#!/usr/bin/env bash
# MusicVault macOS 发布（单架构；Universal 需本机 lipo 合并）
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
RUNTIME="${1:-osx-arm64}"
OUT="${ROOT}/publish/${RUNTIME}"
AOT="${AOT:-false}"
NATIVE_SRC="${ROOT}/src/MusicVault.App/native/macOS/MusicVaultPlayer.m"
DYLIB="${OUT}/libMusicVaultPlayer.dylib"
VLC_APP="${VLC_APP:-/Applications/VLC.app}"

case "$RUNTIME" in
  osx-arm64) LIBVLC_PLATFORM="osx-arm64" ;;
  osx-x64)   LIBVLC_PLATFORM="osx-x64" ;;
  *)
    echo "错误: 不支持的 RUNTIME=${RUNTIME}（期望 osx-arm64 / osx-x64）" >&2
    exit 1
    ;;
esac

echo "==> MusicVault macOS publish (${RUNTIME})"

# ── 1. 发布 .NET 应用（先 publish，再编译 dylib，避免输出目录被 dotnet 清空） ──
ARGS=(
  publish "$ROOT/src/MusicVault.App/MusicVault.App.csproj"
  -c Release
  -r "$RUNTIME"
  --self-contained true
  -o "$OUT"
)

if [[ "$AOT" == "true" ]]; then
  ARGS+=(-p:EnableAot=true)
  echo "发布模式: NativeAOT"
else
  echo "发布模式: 自包含 + Trim（推荐）"
fi

dotnet "${ARGS[@]}"

APP="${OUT}/MusicVault"
if [[ ! -f "$APP" ]]; then
  echo "错误: 未找到发布产物 ${APP}" >&2
  exit 1
fi

# ── 2. 编译原生音频播放器 dylib（AVFoundation 回退） ──
echo "编译原生播放器: ${NATIVE_SRC}"
if command -v clang &>/dev/null; then
  clang -dynamiclib \
    -framework Foundation \
    -framework AVFoundation \
    -o "${DYLIB}" \
    -install_name @executable_path/libMusicVaultPlayer.dylib \
    "${NATIVE_SRC}" \
    -O2
  echo "原生播放器编译完成: ${DYLIB}"
else
  echo "警告: 未找到 clang，跳过原生播放器编译。请安装 Xcode Command Line Tools:"
  echo "  xcode-select --install"
  echo "macOS 本地播放将不可用（曲库/下载/在线试听仍可用）。"
fi

# ── 3. 旁路捆绑 LibVLC（PublishSingleFile / NuGet Mac 包均不足以完整运行） ──
# LibVLCSharp 探测: libvlc/<osx-*>/lib/{libvlc,libvlccore}.dylib
# 优先: VLC.app（含 arm64）；次选: 构建输出 / NuGet 残余（仅 osx-x64 且常缺 libvlccore）
LIBVLC_DST="${OUT}/libvlc"
LIBVLC_LIB="${LIBVLC_DST}/${LIBVLC_PLATFORM}/lib"
LIBVLC_PLUGINS="${LIBVLC_DST}/${LIBVLC_PLATFORM}/plugins"

bundle_libvlc_from_vlc_app() {
  local app="$1"
  local lib_src="${app}/Contents/MacOS/lib"
  local plug_src="${app}/Contents/MacOS/plugins"
  if [[ ! -d "$lib_src" ]]; then
    return 1
  fi
  echo "Bundling LibVLC from ${app} → libvlc/${LIBVLC_PLATFORM}/ ..."
  rm -rf "$LIBVLC_DST"
  mkdir -p "$LIBVLC_LIB"
  # 复制 dylib 与常见依赖；排除巨大无关物
  cp -R "$lib_src/." "$LIBVLC_LIB/"
  if [[ -d "$plug_src" ]]; then
    mkdir -p "$LIBVLC_PLUGINS"
    cp -R "$plug_src/." "$LIBVLC_PLUGINS/"
    # 音频产品：去掉部分视频/GUI 插件以缩小体积（失败也不阻断）
    local remove_dirs=(
      gui video_output video_chroma video_filter video_splitter
      visualization spu text_renderer stream_out access_output mux
      services_discovery lua
    )
    local d
    for d in "${remove_dirs[@]}"; do
      rm -rf "${LIBVLC_PLUGINS}/${d}" 2>/dev/null || true
    done
  fi
  return 0
}

bundle_libvlc_from_dir() {
  local src="$1"
  if [[ ! -d "$src" ]]; then
    return 1
  fi
  echo "Bundling LibVLC from build output ${src} ..."
  rm -rf "$LIBVLC_DST"
  mkdir -p "$LIBVLC_DST"
  cp -R "$src/." "$LIBVLC_DST/"
  return 0
}

LIBVLC_BUNDLED=0
if bundle_libvlc_from_vlc_app "$VLC_APP"; then
  LIBVLC_BUNDLED=1
else
  # 构建输出候选（本机曾用非单文件调试构建留下的 libvlc）
  for candidate in \
    "${ROOT}/src/MusicVault.App/bin/Release/net8.0/${RUNTIME}/libvlc" \
    "${ROOT}/src/MusicVault.App/bin/Release/net8.0/libvlc" \
    "${ROOT}/src/MusicVault.Services/bin/Release/net8.0/libvlc"
  do
    if bundle_libvlc_from_dir "$candidate"; then
      LIBVLC_BUNDLED=1
      break
    fi
  done
fi

# NuGet VideoLAN.LibVLC.Mac：仅有 osx-x64/libvlc.dylib，无 libvlccore；仅作 Intel 弱回退提示复制
if [[ "$LIBVLC_BUNDLED" -eq 0 && "$RUNTIME" == "osx-x64" ]]; then
  NUGET_DYLIB="${HOME}/.nuget/packages/videolan.libvlc.mac/3.1.3.1/build/osx-x64/libvlc.dylib"
  if [[ -f "$NUGET_DYLIB" ]]; then
    echo "警告: 使用不完整的 NuGet Mac 原生（无 libvlccore）；建议安装 VLC.app 后设置 VLC_APP 重跑。"
    rm -rf "$LIBVLC_DST"
    mkdir -p "$LIBVLC_LIB"
    cp "$NUGET_DYLIB" "$LIBVLC_LIB/"
    LIBVLC_BUNDLED=1
  fi
fi

if [[ "$LIBVLC_BUNDLED" -eq 1 ]]; then
  if [[ -f "${LIBVLC_LIB}/libvlc.dylib" || -f "${LIBVLC_DST}/libvlc.dylib" || -f "${OUT}/libvlc.dylib" ]]; then
    SIZE_MB="$(du -sm "$LIBVLC_DST" 2>/dev/null | awk '{print $1}')"
    echo "✓ LibVLC bundled under libvlc/${LIBVLC_PLATFORM}/ (~${SIZE_MB:-?} MB)"
  else
    echo "⚠ LibVLC 目录已复制，但未找到 libvlc.dylib；引擎可能回退 AVFoundation。"
  fi
else
  echo "⚠ 未捆绑 LibVLC 原生库。缺省引擎会回退 AVFoundation（无 EQ / 格式较少）。"
  echo "  推荐: 安装 https://www.videolan.org/vlc/ 后执行:"
  echo "    VLC_APP=/Applications/VLC.app ./scripts/publish-macos.sh ${RUNTIME}"
fi

# ── 4. 法务文件（朋友包根目录） ──
for legal in LICENSE NOTICE THIRD_PARTY_LICENSES.md; do
  if [[ -f "${ROOT}/${legal}" ]]; then
    cp "${ROOT}/${legal}" "${OUT}/"
  fi
done
if [[ -f "${ROOT}/docs/LEGAL.md" ]]; then
  mkdir -p "${OUT}/docs"
  cp "${ROOT}/docs/LEGAL.md" "${OUT}/docs/"
fi

# ── 5. 验收 ──
# osx-arm64 朋友包必须含 AVFoundation dylib；LibVLC / VLC.app 不是硬门槛。
if [[ -f "${DYLIB}" ]]; then
  echo "✓ 原生播放器已就绪: ${DYLIB}"
else
  echo "错误: 未找到 ${DYLIB}"
  echo "  请安装 Xcode Command Line Tools 后重跑: xcode-select --install"
  echo "  ./scripts/publish-macos.sh ${RUNTIME}"
  if [[ "$RUNTIME" == "osx-arm64" ]]; then
    exit 1
  fi
fi

VERSION="$(sed -n 's/.*<Version>\([^<]*\)<\/Version>.*/\1/p' "${ROOT}/Directory.Build.props" | head -n 1 | tr -d '[:space:]')"
if [[ -z "$VERSION" ]]; then
  VERSION="0.0.0"
fi

PACK_ZIP="${PACK_ZIP:-true}"
ZIP_PATH="${ROOT}/publish/MusicVault-${VERSION}-${RUNTIME}.zip"
if [[ "$PACK_ZIP" == "true" ]]; then
  mkdir -p "${ROOT}/publish"
  rm -f "${ZIP_PATH}"
  ( cd "$OUT" && zip -r "${ZIP_PATH}" . -x '*.pdb' )
  echo "✓ Zip: ${ZIP_PATH}"
fi

echo "✓ 应用已发布: ${APP}"
echo "  运行: ${APP}"
echo "  数据目录: ~/Library/Application Support/MusicVault"
echo "  发给朋友: ${ZIP_PATH}（须含 libMusicVaultPlayer.dylib；不保证下载）"
