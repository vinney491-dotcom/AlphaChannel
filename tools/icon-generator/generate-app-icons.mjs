import sharp from "sharp";
import { writeFileSync, mkdirSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));
const OUT = resolve(here, "../../src/AlphaChannel.Plugin/Assets/Icons");
const TABLER_VERSION = "3.44.0";
const SIZE = 256;
const STROKE = "2.25";

// App / tile id -> Tabler outline icon name.
// Browse names at https://tabler.io/icons. Avoid brand-* (trademarked logos).
const map = {
  watch: "player-play",
  screen: "device-desktop",
  party: "users",
  friends: "users-group",
  apps: "layout-grid",
  messages: "message-circle",
  "plugin-hub": "puzzle",
  tweeter: "feather",
  invite: "user-plus",
  enjoy: "heart",
};

function recolor(svg) {
  return svg
    .replaceAll('stroke="currentColor"', 'stroke="#ffffff"')
    .replaceAll('fill="currentColor"', 'fill="#ffffff"')
    .replace('stroke-width="2"', `stroke-width="${STROKE}"`)
    .replace('width="24"', `width="${SIZE}"`)
    .replace('height="24"', `height="${SIZE}"`);
}

mkdirSync(OUT, { recursive: true });

let ok = 0;
const failed = [];
const only = new Set(process.argv.slice(2));
for (const [id, icon] of Object.entries(map)) {
  if (only.size > 0 && !only.has(id)) {
    continue;
  }
  try {
    const url = `https://cdn.jsdelivr.net/npm/@tabler/icons@${TABLER_VERSION}/icons/outline/${icon}.svg`;
    const res = await fetch(url);
    if (!res.ok) {
      failed.push(`${id} (${icon}): HTTP ${res.status}`);
      continue;
    }
    const svg = recolor(await res.text());
    const png = await sharp(Buffer.from(svg), { density: 384 })
      .resize(SIZE, SIZE, { fit: "contain", background: { r: 0, g: 0, b: 0, alpha: 0 } })
      .png()
      .toBuffer();
    writeFileSync(resolve(OUT, `${id}.png`), png);
    ok++;
    console.log(`  ${id.padEnd(13)} <- ${icon}`);
  } catch (error) {
    failed.push(`${id} (${icon}): ${error.message}`);
  }
}

console.log(`\nDone: ${ok} icons written to ${OUT}`);
if (failed.length) {
  console.log("FAILED:\n" + failed.map((line) => "  " + line).join("\n"));
  process.exitCode = 1;
}
