#!/usr/bin/env bash
# Build native CLI + Avalonia UI, then launch the UI.
# Usage:
#   ./scripts/native-build-and-run.sh
#   XIV_PATH=~/.xlcore/ffxiv/game/sqpack/ffxiv ./scripts/native-build-and-run.sh
#   ./scripts/native-build-and-run.sh --doctor-only
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet SDK not found. Install .NET 8: https://dotnet.microsoft.com/download/dotnet/8.0" >&2
  exit 1
fi

echo "==> Building native solution..."
dotnet build AlphaChannel.TexTools.Native.sln -c Release

CLI="$ROOT/ConsoleTools/bin/Release/net8.0/ConsoleTools"
UI="$ROOT/AlphaChannel.TexTools.UI/bin/Release/net8.0/AlphaChannel.TexTools.UI"

echo "==> Doctor"
"$CLI" /doctor

if [[ "${1:-}" == "--doctor-only" ]]; then
  exit 0
fi

if [[ -n "${XIV_PATH:-}" ]]; then
  echo "==> XIV_PATH=$XIV_PATH"
else
  echo "==> XIV_PATH unset (set it to your sqpack/ffxiv dir for mod commands)"
fi

echo "==> Starting Avalonia UI: $UI"
exec "$UI"
