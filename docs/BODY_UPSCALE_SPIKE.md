# Body remapping R&D spike (not a product feature yet)

## Goal

Explore converting clothing meshes between body families (e.g. Bibo ↔ TBSE) using:

1. Canonical body reference meshes per family
2. Comparative data from **already-ported outfits** (same item on body A and body B)

This is **research only**. Do not ship “one-click any body including Lalafell” until a held-out pair validates.

## What TexTools already does

- `ModelModifiers.RaceConvert*` / PDB deform: same topology, bone-matrix squash/stretch between **vanilla races**
- Item Converter / `RootCloner`: file clone + optional race deform — **not** custom body remesh

## Proposed spike steps

1. Pick one body pair with many dual ports (recommended: Bibo ↔ TBSE).
2. Collect N paired gear MDLs you have rights to use + both body refs.
3. Filter pairs (similar vert counts, overlapping bone sets, UV-family checks).
4. Fit a transfer (wrap via body refs, or bone-local offsets).
5. Validate on a held-out paired outfit (wearable silhouette, not pixel-identical).
6. Only then consider UI.

## Non-goals for this spike

- Universal auto-upscale
- Dance / animation
- Helix 3D viewport parity
- Training on mods without redistribution rights

## Status

Documented as spike; **no converter UI shipped** in the Avalonia shell yet.
