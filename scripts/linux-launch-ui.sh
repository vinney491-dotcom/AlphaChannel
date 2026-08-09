#!/usr/bin/env bash
# Quiet launcher for the native Avalonia UI (desktop / menu entry).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

UI_DIR="$ROOT/AlphaChannel.TexTools.UI/bin/Release/net8.0"
UI_BIN="$UI_DIR/AlphaChannel.TexTools.UI"
UI_DLL="$UI_DIR/AlphaChannel.TexTools.UI.dll"

need_build=0
if [[ ! -f "$UI_DLL" && ! -x "$UI_BIN" ]]; then
  need_build=1
elif [[ -n "$(find "$ROOT/AlphaChannel.TexTools.UI" "$ROOT/ConsoleTools" "$ROOT/lib/xivModdingFramework" \
      -name '*.cs' -newer "$UI_DLL" 2>/dev/null | head -n1)" ]]; then
  need_build=1
fi

if [[ "$need_build" -eq 1 ]]; then
  if ! command -v dotnet >/dev/null 2>&1; then
    notify-send "AlphaChannel TexTools" "dotnet SDK not found. Install dotnet-sdk-8.0." 2>/dev/null || true
    echo "dotnet SDK not found" >&2
    exit 1
  fi
  notify-send "AlphaChannel TexTools" "Building… (first launch)" 2>/dev/null || true
  dotnet build "$ROOT/AlphaChannel.TexTools.Native.sln" -c Release >/tmp/alphachannel-textools-build.log 2>&1 \
    || { notify-send "AlphaChannel TexTools" "Build failed — see /tmp/alphachannel-textools-build.log" 2>/dev/null || true; exit 1; }
fi

# Prefer apphost; fall back to `dotnet dll` if the native host is missing.
if [[ -x "$UI_BIN" ]]; then
  exec "$UI_BIN" "$@"
fi
exec dotnet "$UI_DLL" "$@"
