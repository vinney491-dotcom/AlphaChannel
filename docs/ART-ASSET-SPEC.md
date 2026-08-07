# AlphaChannel art asset specification

Stencil icons for Home capability tiles and the Apps launcher. Modeled on Aetherphone's
Tabler-based pipeline so assets drop in without engine changes.

If this disagrees with the code, the code wins. Authoritative draw path:
`AppIconTextures.cs`. Generator: `tools/icon-generator/`.

---

## App / tile icons

| | |
|---|---|
| Canvas | **256 x 256 px** |
| Format | PNG-32, RGBA, 8 bits per channel |
| Colour | **Pure white (255,255,255) everywhere. Shape lives in the alpha channel.** |
| Safe area | Glyph fills the canvas edge to edge; the plugin insets it to **62%** of the disc |
| File name | `<id>.png`, lowercase, kebab-case if needed (`plugin-hub.png`) |
| Location | `src/AlphaChannel.Plugin/Assets/Icons/` |
| Size budget | Prefer **<= 8 KB** |

### Icons are stencils, not artwork

Every shipped icon is white RGB with shape in alpha. At draw time the engine multiplies
by a tint colour, so the mark follows theme/accent. Full-colour icons fight the tint.

- No gradients, no multi-colour marks.
- Soft alpha edges are fine (50% alpha becomes 50% tint).
- Do not pad the PNG; fill the 256 px canvas edge to edge.

### Style

Tabler-derived line iconography: even stroke, geometric, functional.

- Stroke roughly **18-22 px** at 256 px (generator uses Tabler outline + stroke 2.25).
- Rounded caps and joins.
- Nothing finer than **10 px**.

### Export / regenerate

```sh
cd tools/icon-generator
npm install
npm run build
```

Or author by hand: white stencil PNG, then optional `oxipng -o 4 -s`.

### Current ids

| Id | Used on | Tabler source |
|---|---|---|
| `watch` | Home: Watch Videos | `player-play` |
| `screen` | Home: Place the Screen | `device-desktop` |
| `party` | Home: Start a Party | `users` |
| `friends` | Home: Friends | `users-group` |
| `apps` | Home: Apps | `layout-grid` |
| `messages` | Apps: Alpha Chat | `message-circle` |
| `plugin-hub` | Apps: Plugin Hub | `puzzle` |
| `tweeter` | Apps: Tweeter | `feather` |
| `invite` | How it works: Invite (corner accent) | `user-plus` |
| `enjoy` | How it works: Enjoy (corner accent) | `heart` |
| `watch` | Also How it works: Pick Something accent | `player-play` |

Chrome controls (play/pause, search, chevrons, volume) stay on **FontAwesome** via Dalamud
unless you intentionally replace them later.

### Fonts

The plugin already ships Inter (`Fonts/*.ttf`) for UI type, same family Aetherphone uses.
Keep Inter OFL notice next to the fonts.
