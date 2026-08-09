#!/usr/bin/env bash
# Open a native Linux folder picker (Dolphin/KDE via kdialog, else zenity).
# Prints the selected absolute path to stdout. Exit 1 on cancel / no dialog.
set -euo pipefail

START="${1:-$HOME}"
TITLE="${2:-Select FFXIV install folder}"

if [[ ! -d "$START" ]]; then
  START="$HOME"
fi

if command -v kdialog >/dev/null 2>&1; then
  # Plasma file dialog — same stack Dolphin uses; shows hidden folders.
  kdialog --getexistingdirectory "$START" "$TITLE" && exit 0
  exit 1
fi

if command -v zenity >/dev/null 2>&1; then
  zenity --file-selection --directory --filename="${START}/" --title="$TITLE" && exit 0
  exit 1
fi

echo "No native folder picker found. Install kdialog (KDE/Dolphin) or zenity." >&2
echo "  sudo pacman -S --needed kdialog" >&2
exit 1
