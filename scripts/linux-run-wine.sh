#!/usr/bin/env bash
# Run a Windows TexTools build under Wine against a Linux FFXIV / XIVLauncher.Core install.
set -euo pipefail

PREFIX="${WINEPREFIX:-$HOME/.wine-textools}"
TEXTOOLS_DIR="${TEXTOOLS_DIR:-$HOME/tools/textools}"
EXE="${TEXTOOLS_EXE:-$TEXTOOLS_DIR/FFXIV_TexTools.exe}"

# Resolve FFXIV sqpack path (Linux) → Wine Z:\ path for onboarding text box / settings.
resolve_xiv_path() {
  local p="${XIV_PATH:-}"
  if [[ -z "$p" && -f "$HOME/.xlcore/launcher.ini" ]]; then
    p="$(grep -i '^GamePath=' "$HOME/.xlcore/launcher.ini" | head -n1 | cut -d= -f2- | tr -d '"' | sed 's:/*$::')"
    if [[ -n "$p" && -d "$p/game/sqpack/ffxiv" ]]; then
      p="$p/game/sqpack/ffxiv"
    elif [[ -n "$p" && -d "$p/sqpack/ffxiv" ]]; then
      p="$p/sqpack/ffxiv"
    fi
  fi
  if [[ -z "$p" ]]; then
    p="$HOME/.xlcore/ffxiv/game/sqpack/ffxiv"
  fi
  echo "$p"
}

linux_to_wine_z() {
  local linux_path
  linux_path="$(readlink -f "$1" 2>/dev/null || echo "$1")"
  # Wine exposes Unix / as Z:\
  echo "Z:${linux_path//\//\\}"
}

if [[ ! -f "$EXE" ]]; then
  echo "TexTools exe not found at: $EXE" >&2
  echo "Run: ./scripts/linux-install-release.sh" >&2
  exit 1
fi

export WINEPREFIX="$PREFIX"
export WINEARCH="${WINEARCH:-win64}"
export TT_SOFTWARE_RENDERING="${TT_SOFTWARE_RENDERING:-1}"

if [[ ! -d "$PREFIX/drive_c/windows" ]]; then
  echo "Creating Wine prefix at $PREFIX ..."
  wineboot -u
fi

if [[ ! -f "$PREFIX/drive_c/windows/Fonts/arial.ttf" ]]; then
  echo "Installing corefonts + .NET 4.8 (first run, slow)..."
  winetricks -q corefonts fontsmooth=rgb dotnet48 d3dcompiler_47
  winetricks -q --force vcrun2022 || true
fi

XIV_LINUX="$(resolve_xiv_path)"
XIV_WINE="$(linux_to_wine_z "$XIV_LINUX")"

echo "=========================================="
echo "FFXIV path (Linux): $XIV_LINUX"
echo "Paste this into TexTools 'FFXIV Install' box"
echo "(you do NOT need the folder picker / hidden files):"
echo ""
echo "  $XIV_WINE"
echo ""
echo "Or click into the text field and Ctrl+V after:"
echo "  echo -n '$XIV_WINE' | xclip -selection clipboard 2>/dev/null || true"
echo "=========================================="

# Best-effort: copy path to clipboard if possible
if command -v xclip >/dev/null 2>&1; then
  echo -n "$XIV_WINE" | xclip -selection clipboard || true
  echo "(copied to clipboard)"
elif command -v wl-copy >/dev/null 2>&1; then
  echo -n "$XIV_WINE" | wl-copy || true
  echo "(copied to clipboard)"
fi

echo "WINEPREFIX=$WINEPREFIX"
echo "Launching $EXE"
exec wine "$EXE" "$@"
