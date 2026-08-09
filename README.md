# AlphaChannel TexTools

Linux-native (and cross-platform) fork of [FFXIV TexTools](https://github.com/TexTools/FFXIV_TexTools_UI) /
[xivModdingFramework](https://github.com/TexTools/xivModdingFramework), owned alongside
**AlphaChannel** but **not** part of the Dalamud plugin.

**License:** GNU GPL v3.0 — see [LICENSE](LICENSE).

## Run natively (Linux)

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

### What works in the Avalonia shell (Penumbra-first)

| Feature | Status |
|---------|--------|
| Game path / XLCore doctor | Done |
| Import modpack → Penumbra library (DT upgrade + folder) | Done |
| Upgrade / resave / batch-upgrade modpacks | Done |
| Extract by internal path, list root files | Done |
| Open Penumbra / ModPacks folders | Done |
| Index backup status / folder | Done (create/restore still DAT-write / classic) |
| 3D viewers, material editors, live DAT modlist | Not yet (WPF/Helix) |

### CachyOS / Arch

```bash
sudo pacman -S --needed dotnet-sdk-8.0 git base-devel kdialog

cd ~/AlphaChannel-TexTools   # export branch or clone
git fetch origin cursor/textools-export-02c5
git reset --hard origin/cursor/textools-export-02c5

./scripts/native-build-and-run.sh

# Clickable Desktop + app-menu icon
./scripts/install-desktop-launcher.sh
```

Point CLI tools at your XIVLauncher.Core install when you use mod commands:

```bash
export XIV_PATH="$HOME/.xlcore/ffxiv/game/sqpack/ffxiv"
./ConsoleTools/bin/Release/net8.0/ConsoleTools /upgrade ~/Downloads/some.ttmp2 ~/Downloads/some-upgraded.ttmp2
```

Full classic WPF UI (Wine) if you need it before Avalonia parity lands:

```bash
sudo pacman -S --needed wine winetricks unzip curl cabextract
./scripts/linux-install-release.sh
./scripts/linux-run-wine.sh
```

### Build manually

```bash
# Build CLI + Avalonia shell
dotnet build AlphaChannel.TexTools.Native.sln -c Release

# Native CLI doctor (path detection, no game install required)
./ConsoleTools/bin/Release/net8.0/ConsoleTools /doctor

# CLI help
./ConsoleTools/bin/Release/net8.0/ConsoleTools /?

# Point at your XIVLauncher.Core game files, then use modpack commands
export XIV_PATH="$HOME/.xlcore/ffxiv/game/sqpack/ffxiv"
./ConsoleTools/bin/Release/net8.0/ConsoleTools /upgrade ./old.ttmp2 ./new.ttmp2

# Native Avalonia UI shell (path detection today; full TexTools UI parity WIP)
./AlphaChannel.TexTools.UI/bin/Release/net8.0/AlphaChannel.TexTools.UI
```

Or:

```bash
dotnet run --project ConsoleTools -- /doctor
dotnet run --project AlphaChannel.TexTools.UI
```

### What works natively today

| Surface | Status |
|---------|--------|
| **ConsoleTools** (`.NET 8`) | Runs natively on Linux — `/doctor`, `/upgrade`, `/resave`, `/extract`, `/wrap`, `/unwrap`, `/list` |
| **xivModdingFramework** | Builds & runs under .NET 8 on Linux; auto-detects `~/.xlcore` |
| **AlphaChannel.TexTools.UI** (Avalonia) | Native window shell with path detection — **not** full TexTools feature parity yet |
| **Legacy WPF UI** (`FFXIV_TexTools`) | Still Windows / Wine only |

Some model/texture ops still shell out to Windows helper `.exe`s (`texconv`, FBX `converter`, AssetCc). On Linux those are resolved via `NativeConverters` and can be wrapped with Wine (`TEXTOOLS_WINE` / `wine` on `PATH`) until native replacements ship.

## Wine fallback (full classic UI)

If you need the complete WPF TexTools UI today:

```bash
sudo apt install wine winetricks unzip curl cabextract
./scripts/linux-install-release.sh
# Opens Dolphin/KDE (kdialog) to pick the game folder — not Wine's Windows dialog.
# Needs: sudo pacman -S --needed kdialog
./scripts/linux-run-wine.sh   # installs corefonts + .NET 4.8 on first run
# TEXTOOLS_PICK_MODE=pick|ask|auto  (default ask)
```

## Layout

```
AlphaChannel.TexTools.Native.sln   # native CLI + Avalonia
ConsoleTools/                      # .NET 8 native CLI
AlphaChannel.TexTools.UI/          # Avalonia native shell
lib/xivModdingFramework/           # shared core
FFXIV_TexTools/                    # legacy WPF (Windows)
scripts/                           # Wine helpers
```

## Upstream snapshots

| Component | Upstream | Ref |
|-----------|----------|-----|
| UI + ConsoleTools | `TexTools/FFXIV_TexTools_UI` | `6f4ababa…` |
| Framework | `TexTools/xivModdingFramework` | `8e2a260…` (`develop`) |

See [UPSTREAM.md](UPSTREAM.md).

## Related

- [AlphaChannel](https://github.com/vinney491-dotcom/AlphaChannel) — separate Dalamud plugin
