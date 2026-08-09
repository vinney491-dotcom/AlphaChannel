#!/usr/bin/env bash
# Run a Windows TexTools build under Wine against a Linux FFXIV / XIVLauncher.Core install.
#
# Folder picking uses Dolphin/KDE (kdialog) — never Wine's Windows sandbox browser.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PREFIX="${WINEPREFIX:-$HOME/.wine-textools}"
TEXTOOLS_DIR="${TEXTOOLS_DIR:-$HOME/tools/textools}"
EXE="${TEXTOOLS_EXE:-$TEXTOOLS_DIR/FFXIV_TexTools.exe}"
# auto | pick | ask
#   auto — use XIV_PATH / launcher.ini (open Dolphin only if missing)
#   pick — always open Dolphin/kdialog
#   ask  — if we resolved a path, confirm; otherwise pick (default)
PICK_MODE="${TEXTOOLS_PICK_MODE:-ask}"

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

normalize_to_sqpack() {
  local p="$1"
  p="${p%/}"
  if [[ -d "$p/game/sqpack/ffxiv" ]]; then
    echo "$p/game/sqpack/ffxiv"
  elif [[ -d "$p/sqpack/ffxiv" ]]; then
    echo "$p/sqpack/ffxiv"
  else
    echo "$p"
  fi
}

game_root_from_sqpack() {
  # .../game/sqpack/ffxiv → install root (parent of game/)
  local p
  p="$(readlink -f "$1" 2>/dev/null || echo "$1")"
  if [[ "$p" == */game/sqpack/ffxiv ]]; then
    echo "${p%/game/sqpack/ffxiv}"
  elif [[ "$p" == */sqpack/ffxiv ]]; then
    echo "$(dirname "$(dirname "$p")")"
  else
    echo "$p"
  fi
}

linux_to_wine_z() {
  local linux_path
  linux_path="$(readlink -f "$1" 2>/dev/null || echo "$1")"
  echo "Z:${linux_path//\//\\}"
}

pick_with_dolphin() {
  local start="$1"
  if [[ ! -x "$SCRIPT_DIR/linux-pick-folder.sh" ]]; then
    chmod +x "$SCRIPT_DIR/linux-pick-folder.sh" 2>/dev/null || true
  fi
  if [[ ! -d "$start" ]]; then
    start="$HOME"
  fi
  # Start one level up from sqpack when possible so Dolphin shows the install tree.
  if [[ "$start" == */sqpack/ffxiv ]]; then
    start="$(dirname "$(dirname "$(dirname "$start")")")"
  fi
  "$SCRIPT_DIR/linux-pick-folder.sh" "$start" "Select FFXIV install (Dolphin / KDE)"
}

seed_wine_launcher_config() {
  # So Wine TexTools GetDefaultInstallDirectory() finds the game without browsing.
  local game_root_linux="$1"
  local game_root_wine
  game_root_wine="$(linux_to_wine_z "$game_root_linux")"
  local wine_user
  wine_user="$(basename "$(ls -d "$PREFIX/drive_c/users/"* 2>/dev/null | grep -v -E 'Public|Default' | head -n1 || true)")"
  if [[ -z "$wine_user" ]]; then
    wine_user="$(whoami)"
  fi
  local xl_dir="$PREFIX/drive_c/users/$wine_user/AppData/Roaming/XIVLauncher"
  mkdir -p "$xl_dir"
  # Windows XIVLauncher shape — GamePath is the install root.
  cat >"$xl_dir/launcherConfigV3.json" <<EOF
{
  "GamePath": "${game_root_wine//\\/\\\\}"
}
EOF
  echo "Seeded Wine XIVLauncher GamePath → $game_root_wine"
}

seed_wine_user_config() {
  local sqpack_wine="$1"
  local wine_user
  wine_user="$(basename "$(ls -d "$PREFIX/drive_c/users/"* 2>/dev/null | grep -v -E 'Public|Default' | head -n1 || true)")"
  [[ -z "$wine_user" ]] && return 0

  local base="$PREFIX/drive_c/users/$wine_user/AppData/Local/FFXIV_TexTools"
  [[ -d "$base" ]] || return 0

  # Escape for XML text
  local xml_path="${sqpack_wine//&/&amp;}"

  while IFS= read -r -d '' cfg; do
    python3 - "$cfg" "$xml_path" <<'PY'
import sys
from pathlib import Path
import xml.etree.ElementTree as ET

cfg = Path(sys.argv[1])
path = sys.argv[2]
try:
    root = ET.parse(cfg).getroot()
except ET.ParseError:
    sys.exit(0)

settings = root.find("./userSettings/FFXIV_TexTools.Properties.Settings")
if settings is None:
    sys.exit(0)

target = None
for setting in settings.findall("setting"):
    if setting.get("name") == "FFXIV_Directory":
        target = setting
        break

if target is None:
    target = ET.SubElement(settings, "setting", {"name": "FFXIV_Directory", "serializeAs": "String"})
    ET.SubElement(target, "value")

value = target.find("value")
if value is None:
    value = ET.SubElement(target, "value")
value.text = path

# Preserve declaration / indentation lightly
tree = ET.ElementTree(root)
ET.indent(tree, space="    ")
cfg.write_text(
    '<?xml version="1.0" encoding="utf-8"?>\n' + ET.tostring(root, encoding="unicode") + "\n",
    encoding="utf-8",
)
print(f"Updated {cfg}")
PY
  done < <(find "$base" -type f -name user.config -print0 2>/dev/null)
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
if [[ -d "$XIV_LINUX" ]]; then
  XIV_LINUX="$(normalize_to_sqpack "$(readlink -f "$XIV_LINUX")")"
fi

case "$PICK_MODE" in
  pick)
    echo "Opening Dolphin/KDE folder picker…"
    if picked="$(pick_with_dolphin "${XIV_LINUX:-$HOME}")"; then
      XIV_LINUX="$(normalize_to_sqpack "$picked")"
    else
      echo "Folder pick cancelled." >&2
      exit 1
    fi
    ;;
  ask)
    if [[ -d "$XIV_LINUX" ]] && ls "$XIV_LINUX"/*.win32.index >/dev/null 2>&1; then
      echo "Detected FFXIV: $XIV_LINUX"
      if command -v kdialog >/dev/null 2>&1; then
        if kdialog --yesnocancel "Use this FFXIV install?\n\n$XIV_LINUX\n\nYes = use it\nNo = pick another folder in Dolphin\nCancel = quit"; then
          : # yes — keep XIV_LINUX
        else
          status=$?
          if [[ $status -eq 1 ]]; then
            echo "Opening Dolphin/KDE folder picker…"
            picked="$(pick_with_dolphin "$XIV_LINUX")" || { echo "Cancelled." >&2; exit 1; }
            XIV_LINUX="$(normalize_to_sqpack "$picked")"
          else
            echo "Cancelled." >&2
            exit 1
          fi
        fi
      else
        echo "Opening folder picker (install kdialog for Dolphin/KDE dialogs)…"
        if picked="$(pick_with_dolphin "$XIV_LINUX")"; then
          XIV_LINUX="$(normalize_to_sqpack "$picked")"
        fi
      fi
    else
      echo "No valid sqpack path yet — opening Dolphin/KDE folder picker…"
      echo "(Tip: sudo pacman -S --needed kdialog)"
      picked="$(pick_with_dolphin "${XIV_LINUX:-$HOME}")" || { echo "Cancelled." >&2; exit 1; }
      XIV_LINUX="$(normalize_to_sqpack "$picked")"
    fi
    ;;
  auto)
    if [[ ! -d "$XIV_LINUX" ]] || ! ls "$XIV_LINUX"/*.win32.index >/dev/null 2>&1; then
      echo "Auto path missing/invalid — opening Dolphin/KDE folder picker…"
      picked="$(pick_with_dolphin "${XIV_LINUX:-$HOME}")" || { echo "Cancelled." >&2; exit 1; }
      XIV_LINUX="$(normalize_to_sqpack "$picked")"
    fi
    ;;
  *)
    echo "Unknown TEXTOOLS_PICK_MODE=$PICK_MODE (use auto|pick|ask)" >&2
    exit 1
    ;;
esac

XIV_LINUX="$(readlink -f "$XIV_LINUX" 2>/dev/null || echo "$XIV_LINUX")"
XIV_WINE="$(linux_to_wine_z "$XIV_LINUX")"
GAME_ROOT="$(game_root_from_sqpack "$XIV_LINUX")"

if [[ ! -d "$XIV_LINUX" ]] || ! ls "$XIV_LINUX"/*.win32.index >/dev/null 2>&1; then
  echo "Warning: '$XIV_LINUX' does not look like sqpack/ffxiv (no *.win32.index)." >&2
  echo "Continue anyway; TexTools may reject it." >&2
fi

seed_wine_launcher_config "$GAME_ROOT"
seed_wine_user_config "$XIV_WINE"

# Best-effort clipboard (if they still open Settings → browse by mistake)
if command -v xclip >/dev/null 2>&1; then
  echo -n "$XIV_WINE" | xclip -selection clipboard || true
elif command -v wl-copy >/dev/null 2>&1; then
  echo -n "$XIV_WINE" | wl-copy || true
fi

echo "=========================================="
echo "FFXIV path (Linux): $XIV_LINUX"
echo "FFXIV path (Wine):  $XIV_WINE"
echo "Folder pick used Dolphin/KDE (kdialog) — not Wine's Windows dialog."
echo "Onboarding should already show this path; you can Confirm without Browse."
echo "=========================================="
echo "WINEPREFIX=$WINEPREFIX"
echo "Launching $EXE"
exec wine "$EXE" "$@"
