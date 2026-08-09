#!/usr/bin/env bash
# One-shot native install for CachyOS / Arch.
set -euo pipefail

DEST="${ALPHACHANNEL_TEXTOOLS_DIR:-$HOME/AlphaChannel-TexTools}"
EXPORT_URL="https://github.com/vinney491-dotcom/AlphaChannel.git"
EXPORT_BRANCH="cursor/textools-export-02c5"
STANDALONE_URL="https://github.com/vinney491-dotcom/AlphaChannel-TexTools.git"

echo "==> Installing packages"
sudo pacman -S --needed --noconfirm dotnet-sdk-8.0 git base-devel

if [[ -f "$DEST/AlphaChannel.TexTools.Native.sln" ]]; then
  echo "==> Using existing tree at $DEST"
  cd "$DEST"
  if [[ -d .git ]]; then git pull --ff-only || true; fi
else
  mkdir -p "$(dirname "$DEST")"
  echo "==> Trying standalone repo..."
  if git clone --depth 1 "$STANDALONE_URL" "$DEST" 2>/tmp/tt-clone.err; then
    echo "Cloned standalone repo"
  else
    echo "==> Standalone missing; cloning export branch $EXPORT_BRANCH"
    rm -rf "$DEST"
    git clone --depth 1 --branch "$EXPORT_BRANCH" --single-branch "$EXPORT_URL" "$DEST"
  fi
  cd "$DEST"
fi

chmod +x scripts/*.sh
echo "==> Building + launching native UI"
exec ./scripts/native-build-and-run.sh
