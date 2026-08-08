# Upstream provenance

Imported into AlphaChannel for a Linux-oriented fork. Original authors and TexTools
GitHub Group retain copyright; this copy remains under GPL-3.0.

## Sources

- https://github.com/TexTools/FFXIV_TexTools_UI @ `6f4ababa2fc9a1f71c19f86296b92e0a3cc75214`
- https://github.com/TexTools/xivModdingFramework @ `8e2a2603f963ceb38062798c128b7f4efd966e11` (develop)

## AlphaChannel changes (initial)

- Vendored framework instead of git submodule
- `PlatformPaths` helper for XIVLauncher.Core / XDG TexTools data dirs
- `PenumbraAPI` / onboarding defaults use those paths
- `Dat.GetMaximumDatSize` uses `Path.GetPathRoot` (Linux-safe)
- `CompressWindowsDirectory` no-ops off Windows
- Dropped unused `System.Management` package reference
