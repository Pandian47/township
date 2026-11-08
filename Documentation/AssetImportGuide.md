# Asset Import Guide

How to bring AI-generated (or hand-made) art into the project so it lines up with the grid and sorts correctly.

## 1. Global conventions

| Setting | Value |
|---|---|
| Pixels Per Unit | **128** for every world sprite |
| Projection | 2:1 isometric (dimetric). A ground diamond is twice as wide as it is tall |
| Ground tile | **128 × 64 px** diamond, pivot **center (0.5, 0.5)** |
| Building footprint (w × h cells) | Diamond base **(w + h) × 64 px wide**, **(w + h) × 32 px tall**. The image is taller because of the walls and roof |
| Building pivot | **Bottom corner of the footprint diamond** = `(h / (w + h), 0)` normalised. In pixels: x = h × 64, y = 0 |
| Light direction | From the upper left. Left faces are brighter, right faces are darker |
| Background | Transparent PNG, no drop shadow outside the footprint area |
| Texture import | Sprite (2D and UI), Single, Mesh Type Tight (buildings) or Full Rect (tiles), no mipmaps, Bilinear, Clamp |
| Android override | ASTC 6×6, max size 2048 |

Example sizes:

| Building | Footprint | Base width | Pivot X |
|---|---|---|---|
| Silo | 1 × 1 | 128 px | 0.5 |
| Cottage, Barn, Bakery | 2 × 2 | 256 px | 0.5 |
| Town Hall | 3 × 3 | 384 px | 0.5 |
| Warehouse (example) | 3 × 2 | 320 px | 0.4 |

The image width must equal the base width exactly. Otherwise the footprint corners won't sit on the grid.
The height is free (the walls and roof stick up).

## 2. Replacing a placeholder

Placeholders are named `PH_*.png`, for example `Art/Buildings/Production/PH_Building_barn.png`.
The file names keep the `PH_` prefix even after real art is in them; that keeps every reference stable.

### Fastest way: the AI art importer (recommended)

1. Generate images with the prompts in `Documentation/ArtPrompts.md` (flat magenta background, any size).
2. Save them in one folder with the expected names: `cottage, town_hall, barn, bakery, silo, train_station, train_station_train, feed_mill, sugar_mill, creamery, chicken_coop, dairy_shed, field, wheat_1..4,
   corn_1..4, soybean_1..4, sugarcane_1..4, bread, corn_bread, chicken_feed, cow_feed, egg, milk, sugar, cookies, cake, butter, cheese, grass, sand, water, road` (`.png` or `.jpg`). Missing files are skipped and keep their current art.
3. In Unity: **TownBuilder → Art → Import AI Art Folder...** and pick the folder.
   Command line (Unity closed): `"$UNITY" -batchmode -quit -projectPath . -executeMethod TownBuilder.EditorTools.AiArtImporter.ImportFromCommandLine -artSource "D:\path\to\folder"`
4. Run **TownBuilder → Debug → Capture Town Preview** and check the result.

What it does (`Assets/_Game/Editor/AiArtImporter.cs`):
* Removes the magenta background (soft edges, magenta fringe cleaned up) and trims the empty space.
* **Buildings:** measures the footprint width on the lower 40% of the building (walls, not the overhanging roof),
  scales it to (w + h) × 64 px, and sets the pivot on the footprint's bottom corner (centre for square footprints,
  h/(w+h) across for wide ones like the 3×2 Train Station).
* **Field:** whole object scaled to 128 px. **Crops:** plants scaled to 74% of the field and lifted 14 px so they sit in the soil.
* **Icons:** fitted into 128 × 128. **Ground textures:** square top-down texture mapped onto a 128 × 64 diamond (1.5 px overlap, no seams).
* Overwrites the `PH_` PNG and re-applies the standard import settings.

Keep your original images outside the project as a backup; the importer can be re-run any time.

### Manual way

1. Export your new art at the right **width** (table above) with a transparent background.
2. Optional: rename it without the `PH_` prefix. If you rename it, do it **inside Unity** (Project window) so the `.meta` file follows.
3. Easiest path: overwrite the PNG file **outside Unity, using the same file name**. The `.meta` file keeps the GUID, pivot, PPU and import settings, so every reference updates automatically.
4. If the art is a new file instead, select it and apply the settings from section 1 in the Inspector
   (Sprite Mode Single, PPU 128, Pivot Custom, the pivot value from the table). Then assign it to the `BuildingDefinition`'s **Sprite** field.
5. Run **TownBuilder → Debug → Capture Town Preview**. It writes `Temp/TownPreview.png` with a sample town, so you can check alignment and depth sorting.

## 3. New building type

1. Right-click `Assets/_Game/Data/Buildings` → **Create → TownBuilder → Building Definition**.
2. Fill in `id` (lower_snake_case, permanent), name, category, sprite, footprint, unlock level and cost.
3. Add it to `Assets/_Game/ScriptableObjects/GameContent.asset → Buildings`.
4. Press Play. It appears in the Build shop automatically.

## 4. Tiles

1. Put tile PNGs in `Art/Environment/Tiles`, 128 × 64 px, pivot center, PPU 128, Full Rect.
2. Create a Tile asset: drag the sprite into a **Tile Palette** (Window → 2D → Tile Palette) or use **Create → 2D → Tiles → Tile**.
3. Paint on `GroundTilemap` (buildable), `RoadTilemap` and `WaterTilemap` (blocked), or `FarmTilemap` (future farming).

## 5. Validation checklist (per asset)

- [ ] Transparent background, no stray pixels outside the silhouette
- [ ] Correct width for its footprint; diamond base corners touch the image's left, right and bottom edges
- [ ] Same isometric angle as the existing tiles (2:1)
- [ ] Lighting from the upper left, soft shadows only inside the footprint
- [ ] No text, logos or watermarks; nothing copied from existing games
- [ ] Import settings match section 1 (PPU 128, pivot, Android ASTC)
- [ ] Preview capture shows the building sitting exactly on its footprint and sorting correctly against neighbours

## 6. Fonts (TextMeshPro)

The UI uses TextMeshPro with **Liberation Sans SDF** (free, SIL Open Font License). To use your own game font later:

1. Pick a font with a licence that allows embedding in a commercial app (for example any SIL OFL font). Put the `.ttf` in `Assets/_Game/Fonts`.
2. In Unity: right-click the `.ttf` → **Create → TextMeshPro → Font Asset → SDF**. This creates `<Font> SDF.asset` next to it.
3. Select the new font asset. In the Inspector set **Atlas Population Mode** to *Dynamic* (any character works) and keep **Sampling Point Size** at 90.
4. Change `UIFontAssetPath` in `Assets/_Game/Editor/ProjectSetup.cs` to the new asset's path and delete `Assets/_Game/Materials/UIFont Outline.mat`.
5. Close Unity and run setup with `-rebuildScenes` so every label and the outline preset are rebuilt with the new font. Then run the preview capture to check sizes.

If `Assets/TextMesh Pro/` is missing (fresh clone without it), import TMP's resources once before running setup:

```bash
"$UNITY" -batchmode -quit -projectPath . -importPackage "<project>\Library\PackageCache\com.unity.ugui@<hash>\Package Resources\TMP Essential Resources.unitypackage"
```
