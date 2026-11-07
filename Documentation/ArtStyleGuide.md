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

## v0.1 asset list (replace the `PH_` placeholders)

Export each image as a transparent PNG at **exactly** the width shown. Then overwrite the placeholder PNG with the
same file name, outside Unity, so its pivot and import settings are kept (see AssetImportGuide.md). Height can vary
for buildings and plants.

| File (in `Assets/_Game/Art/...`) | Size (px) | Pivot | Prompt subject (insert into the prompt above) |
|---|---|---|---|
| Environment/Tiles/PH_Tile_Grass.png | 128 × 64 | centre | "a single isometric grass ground tile, diamond shaped, short lush grass, tileable edges" |
| Environment/Tiles/PH_Tile_Sand.png | 128 × 64 | centre | "a single isometric sand ground tile, diamond shaped, warm beach sand, tileable edges" |
| Environment/Tiles/PH_Tile_Water.png | 128 × 64 | centre | "a single isometric water tile, diamond shaped, calm clear lake water with soft highlights, tileable edges" |
| Environment/Tiles/PH_Tile_Road.png | 128 × 64 | centre | "a single isometric cobblestone road tile, diamond shaped, light grey stones, tileable edges" |
| Buildings/Farming/PH_Building_field.png | 128 wide | bottom corner (0.5, 0) | "an empty tilled farm field plot, 1x1 tile, dark brown soil rows with a low wooden border" |
| Crops/PH_Crop_wheat_1..4.png | 128 wide | bottom corner (0.5, 0) | "wheat crop on a 1x1 field, growth stage N of 4 (1 = tiny green sprouts, 2 = green stalks, 3 = tall yellow-green, 4 = ripe golden wheat ears)", soil NOT included (transparent ground) |
| Crops/PH_Crop_corn_1..4.png | 128 wide | bottom corner (0.5, 0) | "corn crop on a 1x1 field, growth stage N of 4 (4 = tall plants with yellow cobs)", soil NOT included |
| Products/PH_Product_bread.png | 128 × 128 | centre | "a single loaf of fresh bread, game item icon, front three-quarter view" |
| Products/PH_Product_corn_bread.png | 128 × 128 | centre | "a square golden corn bread, game item icon" |
| Buildings/Community/PH_Building_town_hall.png | 384 wide | (0.5, 0) | "a friendly town hall with a clock, 3x3 tile footprint" |
| Buildings/Residential/PH_Building_cottage.png | 256 wide | (0.5, 0) | "a small cozy cottage with a red roof, 2x2 tile footprint" |
| Buildings/Production/PH_Building_barn.png | 256 wide | (0.5, 0) | "a red wooden farm barn with white trim, 2x2 tile footprint" |
| Buildings/Production/PH_Building_bakery.png | 256 wide | (0.5, 0) | "a warm village bakery with an orange awning and chimney, 2x2 tile footprint" |
| Buildings/Production/PH_Building_silo.png | 128 wide | (0.5, 0) | "a tall metal grain silo, 1x1 tile footprint" |

Crop sprites are drawn on top of the field, so they must leave the ground transparent and keep the same 2:1 diamond
base as the field. Do a test set first: grass tile, field, wheat stage 4 and bread. Then run
**TownBuilder → Debug → Capture Town Preview** before generating the rest.

Rules:
1. Generate **one asset per image**. Never ask for a sheet of 50 objects.
2. Make a small **art test set** first (3 tiles, 3 buildings, 2 crops). Import it and run the preview capture.
   Approve the style before producing the rest.
3. Keep a log of the prompts that produced approved assets in `Documentation/ArtPromptLog.md`.
4. After generation: remove the background, trim it, scale it to the exact footprint width (AssetImportGuide.md), and check the outline thickness.
