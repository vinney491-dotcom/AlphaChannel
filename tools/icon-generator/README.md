# App icon generator

Regenerates home / Apps tile icons in `src/AlphaChannel.Plugin/Assets/Icons/` from
[Tabler Icons](https://tabler.io/icons) (MIT). Same stencil approach as Aetherphone:
white RGB, shape in alpha, tinted at draw time.

## Run

```sh
cd tools/icon-generator
npm install
npm run build
```

This downloads each mapped Tabler outline SVG, recolors it to white, thickens the
stroke slightly for small-size legibility, and rasterizes it to a 256px transparent
PNG named after the tile id (`watch.png`, `messages.png`, …).

## Changing an icon

Edit the `map` (id -> Tabler icon name) in `generate-app-icons.mjs` and re-run.
Browse names at https://tabler.io/icons. Avoid `brand-*` icons.

Pass ids as arguments to regenerate only those icons:

```sh
node generate-app-icons.mjs messages tweeter
```

## Draw path

`AppIconTextures.TryDraw` loads `Assets/Icons/<id>.png` and draws with
`AddImage(..., tint)`. Call sites fall back to FontAwesome when the PNG is missing.

## License

Tabler Icons is MIT licensed; keep the notice in `docs/THIRD-PARTY-ICONS.md`.
