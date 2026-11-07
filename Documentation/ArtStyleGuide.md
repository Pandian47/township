# Art Style Guide

The game's look must be **original**. We share a genre with other farming and town-building games, but we do not
copy any existing game's buildings, characters, UI, logos or colour schemes. Never feed screenshots of other games
to an image generator as references.

## Visual direction

* Colourful, premium, cartoon simulation style: bright and warm, but not neon.
* Fixed 2:1 isometric (dimetric) view, orthographic, with no perspective distortion.
* Light from the **upper left**: left faces lit, right faces about 25–30 % darker, roofs brightest.
* Soft shadows that stay inside the footprint area of the sprite.
* Clean shapes with a subtle darker outline (1–3 px at 128 PPU).
* No pixel art, no photorealism, no text, labels or watermarks inside the art.

## Palette anchors (starting point; refine once the first AI art set is approved)

| Use | Colour |
|---|---|
| Grass | `#76BA52` |
| Sand | `#E8D496` |
| Soil | `#966842` |
| Water | `#48A0DC` |
| Road | `#B0A89E` |
| UI panel | `#FFFAED` |
| UI positive (place, confirm) | `#4DB04F` |
| UI negative (remove, cancel) | `#DB544A` |

## Reusable image-generation prompt

```
Create an original high-quality 2D isometric mobile city-building game asset.

Visual direction: colorful premium cartoon simulation game; consistent 2:1 isometric perspective;
fixed orthographic viewing angle; bright natural colors; soft shadows; clean detailed shapes;
professional mobile game artwork; consistent lighting from the upper left.
No pixel art. No photorealism. No text, labels or watermarks.

Subject: [ONE ASSET, e.g. "a small red wooden barn with a white-trimmed door", footprint 2x2 tiles]

Output: single isolated game asset; transparent background; complete subject visible; no cropping;
the base forms an isometric diamond whose left, right and bottom corners touch the image edges;
suitable for Unity 2D sprite import; consistent style with the rest of the set.
```

Rules:
1. Generate **one asset per image**. Never ask for a sheet of 50 objects.
2. Make a small **art test set** first (3 tiles, 3 buildings, 2 crops). Import it and run the preview capture.
   Approve the style before producing the rest.
3. Keep a log of the prompts that produced approved assets in `Documentation/ArtPromptLog.md`.
4. After generation: remove the background, trim it, scale it to the exact footprint width (AssetImportGuide.md), and check the outline thickness.
