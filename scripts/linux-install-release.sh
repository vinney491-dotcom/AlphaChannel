#!/usr/bin/env bash
# Download the latest upstream TexTools Windows release for use with scripts/linux-run-wine.sh
set -euo pipefail

DEST="${TEXTOOLS_DIR:-$HOME/tools/textools}"
mkdir -p "$DEST"

echo "Fetching latest release asset from TexTools/FFXIV_TexTools_UI ..."
API="https://api.github.com/repos/TexTools/FFXIV_TexTools_UI/releases/latest"

# NOTE: must not use a heredoc on python stdin — that steals curl's pipe.
ASSET_URL=$(curl -fsSL "$API" | python3 -c '
import json,sys
data=json.load(sys.stdin)
assets=data.get("assets") or []
pick=None
for a in assets:
    name=(a.get("name") or "").lower()
    if name.endswith(".zip"):
        pick=a
        break
if pick is None and assets:
    pick=assets[0]
if not pick:
    raise SystemExit("No release assets found")
print(pick["browser_download_url"])
')

# Hardcoded fallback if API is rate-limited / empty
if [[ -z "${ASSET_URL:-}" ]]; then
  ASSET_URL="https://github.com/TexTools/FFXIV_TexTools_UI/releases/download/v3.1.1.4/FFXIV_TexTools_v3.1.1.4b.zip"
  echo "API lookup failed; using fallback $ASSET_URL"
fi

NAME=$(basename "$ASSET_URL")
TMP=$(mktemp -d)
trap 'rm -rf "$TMP"' EXIT
echo "Downloading $NAME -> $DEST"
curl -fL "$ASSET_URL" -o "$TMP/$NAME"

if [[ "$NAME" == *.zip ]]; then
  unzip -o "$TMP/$NAME" -d "$DEST"
else
  cp "$TMP/$NAME" "$DEST/"
  echo "Installer saved to $DEST/$NAME — run it under Wine, or extract a portable zip if available."
fi

echo "Done. Run: TEXTOOLS_DIR=$DEST ./scripts/linux-run-wine.sh"
