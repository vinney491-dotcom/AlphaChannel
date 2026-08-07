# Paste this whole file into Cursor Desktop as the starting prompt
# (or open the PR / agent run linked below).

## Cloud agent run (this chat)

- URL: https://cursor.com/agents/bc-019fdd16-2ef7-7ff5-a35e-870416b6f312
- Repo: vinney491-dotcom/AlphaChannel
- Branch: `cursor/tabler-stencil-icons-f312`
- Goal of this thread: match Aetherphone-style assets for AlphaChannel, then continue on Desktop.

## What we learned about Aetherphone

From https://github.com/XeldarAlz/FFXIV-Aetherphone (releases + docs):

- **App host**: C# Dalamud plugin (`Dalamud.NET.Sdk`), .NET 10
- **UI**: Dear ImGui immediate mode; custom draw-list toolkit (not stock ImGui widgets)
- **Apps**: `IPhoneApp` + `AppSkin` / `AppPalette` / `Metrics` / `Typography`
- **Design assets**:
  - **Inter** font
  - **Tabler Icons** → white stencil PNGs (256×256, RGB white, shape in alpha), tinted at draw
  - Optional Twemoji PNGs for emoji
  - Generator: Node + `sharp` (`tools/icon-generator`)
  - Spec: their `docs/ART-ASSET-SPEC.md`
- Backend "Aethernet" is a separate ASP.NET service (not required for client assets)

## What we did in AlphaChannel (this PR)

1. Added `tools/icon-generator/` (same Tabler → white PNG pipeline).
2. Generated 10 icons into `src/AlphaChannel.Plugin/Assets/Icons/`.
3. Added `AppIconTextures.TryDraw` (tint via `AddImage`, 62% glyph inset).
4. Wired Home capability tiles, How-it-works accents, and Apps launcher tiles to stencil ids with FontAwesome fallback.
5. Documented in `docs/ART-ASSET-SPEC.md` and `docs/THIRD-PARTY-ICONS.md`.
6. csproj copies `Assets/Icons/*.png` to output.

### Icon ids

| Id | Where |
|---|---|
| watch, screen, party, friends, apps | Home capabilities |
| messages, plugin-hub, tweeter | Apps page |
| invite, enjoy | How-it-works corner accents |

### Regenerate

```sh
cd tools/icon-generator
npm install
npm run build
# or: node generate-app-icons.mjs messages
```

## Continue on Desktop

Suggested next prompts (pick one):

1. **Review in-game**: build plugin, reload Dalamud, confirm Home/Apps tiles show tinted Tabler stencils.
2. **More icons**: map sidebar / Player / Settings chrome that should leave FontAwesome.
3. **Typography**: ensure Inter weights are actually shipped under `Fonts/` and used like Aetherphone's `TextStyles`.
4. **Art pass**: custom (non-Tabler) stencils that still obey the white+alpha rules in `docs/ART-ASSET-SPEC.md`.

## Constraints to keep

- White stencil only for themeable glyphs (no full-colour app icons).
- Avoid Tabler `brand-*` icons.
- Keep MIT notice for Tabler.
- Do not invent a second icon system; extend `AppIconTextures` + the generator map.
