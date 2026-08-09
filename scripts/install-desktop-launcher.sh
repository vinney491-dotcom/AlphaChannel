#!/usr/bin/env bash
# Install a clickable Desktop + application-menu launcher for native TexTools UI.
# Usage: ./scripts/install-desktop-launcher.sh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
LAUNCHER="$ROOT/scripts/linux-launch-ui.sh"
ICON_SRC="$ROOT/packaging/alphachannel-textools.png"
APP_ID="alphachannel-textools"
DESKTOP_NAME="AlphaChannel TexTools.desktop"

chmod +x "$ROOT/scripts/"*.sh

ICON_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/icons/hicolor/256x256/apps"
APP_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/applications"
DESKTOP_DIR="$HOME/Desktop"
mkdir -p "$ICON_DIR" "$APP_DIR" "$DESKTOP_DIR"

ICON_DST="$ICON_DIR/${APP_ID}.png"
if [[ -f "$ICON_SRC" ]]; then
  cp -f "$ICON_SRC" "$ICON_DST"
else
  ICON_DST="applications-games"
fi

write_desktop() {
  local out="$1"
  cat >"$out" <<EOF
[Desktop Entry]
Type=Application
Version=1.0
Name=AlphaChannel TexTools
GenericName=FFXIV Modding Tools
Comment=Native Linux TexTools shell (path setup, modpack upgrade)
Exec=${LAUNCHER}
Icon=${APP_ID}
Terminal=false
Categories=Game;Utility;
StartupNotify=true
StartupWMClass=AlphaChannel.TexTools.UI
Keywords=FFXIV;TexTools;modpack;ttmp2;Penumbra;
EOF
  chmod +x "$out"
}

APP_DESKTOP="$APP_DIR/${APP_ID}.desktop"
DESKTOP_FILE="$DESKTOP_DIR/$DESKTOP_NAME"
write_desktop "$APP_DESKTOP"
write_desktop "$DESKTOP_FILE"

# Plasma / GNOME often require "trust" before desktop icons launch.
if command -v gio >/dev/null 2>&1; then
  gio set "$DESKTOP_FILE" metadata::trusted true 2>/dev/null || true
  gio set "$DESKTOP_FILE" "metadata::xfce-exe-checksum" "$(sha256sum "$DESKTOP_FILE" | awk '{print $1}')" 2>/dev/null || true
fi

# Refresh icon / menu caches when tools exist.
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
  gtk-update-icon-cache -f "${XDG_DATA_HOME:-$HOME/.local/share}/icons/hicolor" 2>/dev/null || true
fi
if command -v update-desktop-database >/dev/null 2>&1; then
  update-desktop-database "$APP_DIR" 2>/dev/null || true
fi

echo "Installed launcher:"
echo "  Menu:    $APP_DESKTOP"
echo "  Desktop: $DESKTOP_FILE"
echo "  Icon:    $ICON_DST"
echo ""
echo "On KDE: if Desktop shows it as a text file, right-click → Allow Launching"
echo "Then double-click “AlphaChannel TexTools”."
