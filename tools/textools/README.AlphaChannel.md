# TexTools (AlphaChannel fork)

Vendored fork of [TexTools/FFXIV_TexTools_UI](https://github.com/TexTools/FFXIV_TexTools_UI) and
[TexTools/xivModdingFramework](https://github.com/TexTools/xivModdingFramework) for AlphaChannel
Linux / cross-platform work.

**License:** GNU GPL v3.0 (see `LICENSE`). This tree stays GPL. Do not link it into the
AlphaChannel Dalamud plugin unless you intend to GPL the plugin as well. Treat it as a
separate program under `tools/textools/`.

## Upstream snapshots

| Component | Upstream | Ref |
|-----------|----------|-----|
| UI + ConsoleTools | `TexTools/FFXIV_TexTools_UI` | `6f4ababa2fc9a1f71c19f86296b92e0a3cc75214` (`master`) |
| Framework | `TexTools/xivModdingFramework` | `8e2a2603f963ceb38062798c128b7f4efd966e11` (`develop`, matching upstream submodule) |

Framework is vendored under `lib/xivModdingFramework/` (no git submodule).

## Can it be Linux-compatible?

Yes — but not as a small patch.

| Layer | Today | Linux path |
|-------|--------|------------|
| **UI** (`FFXIV_TexTools`) | .NET Framework 4.8 + **WPF** (MahApps, HelixToolkit.Wpf.SharpDX) | Must be replaced (Avalonia rewrite) or run under Wine/Proton. Native WPF does not exist on Linux. |
| **Framework** (`xivModdingFramework`) | `netstandard2.0`, mostly portable C# | Closest to native Linux. Needs path fixes, native helper binaries, optional package swaps. |
| **ConsoleTools** | `net48` CLI over the framework | Best first native target: retarget to modern .NET, drop `System.Windows` leftovers, ship Linux CLI. |
| **Native helpers** | `texconv.exe`, `converter.exe`, `AssetCc2.exe` / `NotAssetCc.exe` | Need Linux builds or replacements (DirectXTex, open FBX/Havok pipelines, etc.). |

People already run the **Windows binary under Wine** (Lutris / Proton). That is compatibility via Wine, not a native port.

Related prior art: [FFMT](https://ffmt.onrender.com/) — cross-platform CLI aiming at TexTools feature parity, also GPL and built on xivModdingFramework ideas.

## Port plan (this fork)

1. **Foundation (started)** — launcher/user data paths for XIVLauncher.Core (`~/.xlcore`), volume root detection without Windows drive letters, skip Windows-only `compact.exe`.
2. **ConsoleTools on .NET 8+** — headless import/export/list against a Linux game install (`~/.xlcore/ffxiv` or Steam).
3. **Native converters** — inventory every `.exe` spawn; replace or wrap for Linux.
4. **UI** — Avalonia (or similar) shell reusing ViewModels / framework APIs; do not expect MahApps/Helix WPF to port cleanly.
5. **Keep Windows working** — `#if` / runtime OS checks; do not break the upstream Windows workflow while Linux lands.

## Layout

```
tools/textools/
  FFXIV_TexTools/           # WPF UI (Windows for now)
  ConsoleTools/             # CLI entry (Linux priority)
  ForceUpdateAssembly/
  lib/xivModdingFramework/  # Shared modding core
  LICENSE                   # GPL-3.0
  readme.md                 # Upstream readme
  README.AlphaChannel.md    # This file
```

## Building (Windows / status quo)

Same as upstream: Visual Studio, .NET Framework 4.8, open `FFXIV_TexTools.sln`.

## Building toward Linux

Framework target remains `netstandard2.0` for now. Next concrete step is a `net8.0` ConsoleTools project that references the framework and runs on Linux without WPF.

```bash
# From a machine with the .NET SDK once ConsoleTools is retargeted:
dotnet build tools/textools/ConsoleTools/ConsoleTools.csproj
```

Until that retarget lands, use Wine for the full UI, or develop against the framework APIs from a new Linux-friendly host project.
