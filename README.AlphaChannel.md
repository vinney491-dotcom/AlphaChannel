# AlphaChannel TexTools — project notes

This repository is **owned alongside AlphaChannel** but is **not** the AlphaChannel plugin.
It is a standalone GPL-3.0 fork of TexTools for Linux / cross-platform work.

Do not merge this codebase into the AlphaChannel Dalamud plugin unless you intend to
GPL that plugin as well. Keep them separate programs.

## Can it be Linux-compatible?

Yes — but not as a small patch. Details in the root [README.md](README.md).

People already run the Windows binary under Wine/Lutris/Proton; that is not a native port.

Related prior art: [FFMT](https://ffmt.onrender.com/) (cross-platform CLI, also GPL).

## Port plan

1. **Foundation (started)** — launcher/user data paths for XIVLauncher.Core, volume roots, skip `compact.exe` off Windows.
2. **ConsoleTools on .NET 8+** — headless import/export/list against a Linux game install.
3. **Native converters** — replace or wrap `texconv.exe`, `converter.exe`, AssetCc helpers.
4. **UI** — Avalonia (or similar) shell; do not expect MahApps/Helix WPF to port cleanly.
5. **Keep Windows working** — runtime OS checks; do not break the Windows workflow while Linux lands.
