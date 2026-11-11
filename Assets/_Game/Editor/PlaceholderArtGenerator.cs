using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TownBuilder.EditorTools
{
    /// <summary>
    /// Generates PLACEHOLDER sprites that already follow the final art conventions (PPU 128, 2:1 isometric,
    /// building pivot on the bottom footprint corner). Replace a PNG with AI-generated art of the same footprint
    /// and keep the .meta file: pivot, PPU and import settings are preserved.
    /// Existing PNGs are never overwritten.
    /// </summary>
    public static class PlaceholderArtGenerator
    {
        public const string TilesArtFolder = "Assets/_Game/Art/Environment/Tiles";
        public const string TileAssetsFolder = "Assets/_Game/Art/Environment/Tiles/TileAssets";
        public const string BuildingsArtFolder = "Assets/_Game/Art/Buildings";
        public const string UiArtFolder = "Assets/_Game/Art/UI";

        private const int TileWidth = 128;
        private const int TileHeight = 64;
        private const int HalfW = TileWidth / 2;
        private const int HalfH = TileHeight / 2;

        public sealed class BuildingArtSpec
        {
            public string Id;
            public string Folder;
            public int FootprintX;
            public int FootprintY;
            public int HeightPx;
            public Color32 Wall;
            public Color32 Roof;
        }

        public sealed class Result
        {
            public Tile Grass, Sand, Soil, Water, Road;
            public Sprite CellHighlight;
            public readonly Dictionary<string, Sprite> Buildings = new Dictionary<string, Sprite>();
        }

        public const string CellHighlightPath = UiArtFolder + "/PH_CellHighlight.png";

        public static string TileAssetPath(string name) => $"{TileAssetsFolder}/{name}.asset";

        /// <summary>Loads the generated tiles and overlay sprite from disk (building sprites are not needed by scenes).</summary>
        public static Result LoadExisting()
        {
            Tile Load(string name) => AssetDatabase.LoadAssetAtPath<Tile>(TileAssetPath(name))
                                      ?? throw new System.InvalidOperationException($"Missing tile {name}. Run the full setup first.");
            return new Result
            {
                Grass = Load("PH_Tile_Grass"),
                Sand = Load("PH_Tile_Sand"),
                Soil = Load("PH_Tile_Soil"),
                Water = Load("PH_Tile_Water"),
                Road = Load("PH_Tile_Road"),
                CellHighlight = AssetDatabase.LoadAssetAtPath<Sprite>(CellHighlightPath),
            };
        }

        public static Result Generate(IEnumerable<BuildingArtSpec> buildings)
        {
            var result = new Result
            {
                Grass = CreateTile("PH_Tile_Grass", new Color32(118, 186, 82, 255), TileStyle.Grass),
                Sand = CreateTile("PH_Tile_Sand", new Color32(232, 212, 150, 255), TileStyle.Plain),
                Soil = CreateTile("PH_Tile_Soil", new Color32(150, 104, 66, 255), TileStyle.Soil),
                Water = CreateTile("PH_Tile_Water", new Color32(72, 160, 220, 255), TileStyle.Water),
                Road = CreateTile("PH_Tile_Road", new Color32(176, 168, 158, 255), TileStyle.Road),
                CellHighlight = CreateSprite(CellHighlightPath, RenderCellHighlight, new Vector2(0.5f, 0.5f), SpriteMeshType.FullRect),
            };

            foreach (var spec in buildings)
            {
                Vector2 pivot = new Vector2((float)spec.FootprintY / (spec.FootprintX + spec.FootprintY), 0f);
                var path = $"{spec.Folder}/PH_Building_{spec.Id}.png";
                result.Buildings[spec.Id] = CreateSprite(path, () => RenderBuilding(spec), pivot, SpriteMeshType.Tight);
            }

            return result;
        }

        public const string RailTileName = "PH_Tile_Rail";
        public const string LockIconPath = UiArtFolder + "/PH_Icon_Lock.png";

        /// <summary>Railway tile: grass with a gravel bed, sleepers and two rails running along the grid's Y axis (screen up-left).</summary>
        public static Tile CreateRailTile()
        {
            var sprite = CreateSprite($"{TilesArtFolder}/{RailTileName}.png", RenderRailTile, new Vector2(0.5f, 0.5f), SpriteMeshType.FullRect);
            var tilePath = TileAssetPath(RailTileName);
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                tile.colliderType = Tile.ColliderType.None;
                AssetDatabase.CreateAsset(tile, tilePath);
            }

            return tile;
        }

        private static Texture2D RenderRailTile()
        {
            var tex = NewTexture(TileWidth, TileHeight);
            var pixels = new Color32[TileWidth * TileHeight];
            var grass = new Color32(118, 186, 82, 255);
            for (int y = 0; y < TileHeight; y++)
            for (int x = 0; x < TileWidth; x++)
            {
                float dx = (x + 0.5f - HalfW) / HalfW, dy = (y + 0.5f - HalfH) / HalfH;
                if (Mathf.Abs(dx) + Mathf.Abs(dy) > 1.02f) continue;
                float across = (dx + dy) * 0.5f;  // across the track (grid X), -0.5..0.5
                float along = (dy - dx) * 0.5f;   // along the track (grid Y)
                Color32 c = Scale(grass, 1f + (Hash(x, y) - 0.5f) * 0.08f);
                if (Mathf.Abs(across) < 0.26f) c = Scale(new Color32(156, 146, 132, 255), 0.94f + Hash(x * 3, y) * 0.12f); // gravel
                float sleeperPhase = ((along + 1f) * 4f) % 1f;
                if (Mathf.Abs(across) < 0.22f && sleeperPhase < 0.32f) c = new Color32(122, 86, 56, 255);                  // sleepers
                if (Mathf.Abs(Mathf.Abs(across) - 0.13f) < 0.022f) c = new Color32(176, 182, 192, 255);                    // rails
                pixels[y * TileWidth + x] = c;
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>Padlock badge (shown above a landmark that hasn't opened yet).</summary>
        public static Sprite CreateLockIcon() =>
            CreateSprite(LockIconPath, RenderLockIcon, new Vector2(0.5f, 0.5f), SpriteMeshType.FullRect);

        private static Texture2D RenderLockIcon()
        {
            const int size = 96;
            var tex = NewTexture(size, size);
            var pixels = new Color32[size * size];
            var gold = new Color32(240, 186, 56, 255);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f - 48f, py = y + 0.5f;
                bool body = Mathf.Abs(px) < 28f && py > 10f && py < 54f;
                float r = Mathf.Sqrt(px * px + (py - 54f) * (py - 54f));
                bool shackle = py >= 54f && r > 14f && r < 22f;
                if (!body && !shackle) continue;
                Color32 c = shackle ? new Color32(160, 166, 176, 255) : Scale(gold, py > 40f ? 1.08f : 0.95f);
                if (body && px * px + (py - 34f) * (py - 34f) < 30f) c = new Color32(90, 60, 30, 255); // keyhole
                pixels[y * size + x] = c;
            }

            AddOutline(pixels, size, size);
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        public const string LockedTileName = "PH_Tile_Locked";
        public const string SaleSignPath = "Assets/_Game/Art/Environment/PH_Sign_ForSale.png";

        /// <summary>Overlay tile for land the player doesn't own: dark, see-through, with diagonal hatching.</summary>
        public static Tile CreateLockedTile()
        {
            var sprite = CreateSprite($"{TilesArtFolder}/{LockedTileName}.png", RenderLockedTile, new Vector2(0.5f, 0.5f), SpriteMeshType.FullRect);
            var tilePath = TileAssetPath(LockedTileName);
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                tile.colliderType = Tile.ColliderType.None;
                AssetDatabase.CreateAsset(tile, tilePath);
            }

            return tile;
        }

        private static Texture2D RenderLockedTile()
        {
            var tex = NewTexture(TileWidth, TileHeight);
            var pixels = new Color32[TileWidth * TileHeight];
            for (int y = 0; y < TileHeight; y++)
            for (int x = 0; x < TileWidth; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                float d = Mathf.Abs(px - HalfW) / HalfW + Mathf.Abs(py - HalfH) / HalfH;
                if (d > 1.02f) continue; // slight overlap so neighbouring tiles leave no gaps
                bool hatch = ((x + 2 * y) % 16) < 3;
                pixels[y * TileWidth + x] = hatch ? new Color32(20, 26, 22, 150) : new Color32(16, 22, 20, 105);
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>Small wooden "for sale" sign (pivot at the bottom of the post) standing on plots that can be bought.</summary>
        public static Sprite CreateSaleSign() =>
            CreateSprite(SaleSignPath, RenderSaleSign, new Vector2(0.5f, 0f), SpriteMeshType.FullRect);

        private static Texture2D RenderSaleSign()
        {
            const int w = 72, h = 96;
            var tex = NewTexture(w, h);
            var pixels = new Color32[w * h];
            var wood = new Color32(176, 120, 66, 255);
            var board = new Color32(236, 206, 140, 255);
            var coin = new Color32(246, 196, 48, 255);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                bool post = Mathf.Abs(px - 36f) < 4f && py < 60f;
                bool boardArea = px > 6f && px < 66f && py > 48f && py < 90f;
                if (!post && !boardArea) continue;
                Color32 c = post && !boardArea ? Scale(wood, px < 36f ? 1.05f : 0.85f) : board;
                if (boardArea && (px < 10f || px > 62f || py < 52f || py > 86f)) c = wood; // frame
                float dx = px - 36f, dy = py - 69f;
                if (boardArea && dx * dx + dy * dy < 120f) c = dx * dx + dy * dy < 50f ? Scale(coin, 1.1f) : coin;
                pixels[y * w + x] = c;
            }

            AddOutline(pixels, w, h);
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        // ------------------------------------------------------------------ Tiles

        private enum TileStyle { Plain, Grass, Soil, Water, Road }

        private static Tile CreateTile(string name, Color32 baseColor, TileStyle style)
        {
            var sprite = CreateSprite($"{TilesArtFolder}/{name}.png", () => RenderTile(baseColor, style), new Vector2(0.5f, 0.5f), SpriteMeshType.FullRect);
            var tilePath = TileAssetPath(name);
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                tile.colliderType = Tile.ColliderType.None;
                AssetDatabase.CreateAsset(tile, tilePath);
            }
            else if (tile.sprite == null)
            {
                tile.sprite = sprite;
                EditorUtility.SetDirty(tile);
            }

            return tile;
        }

        private static Texture2D RenderTile(Color32 baseColor, TileStyle style)
        {
            var tex = NewTexture(TileWidth, TileHeight);
            var pixels = new Color32[TileWidth * TileHeight];
            for (int y = 0; y < TileHeight; y++)
            {
                for (int x = 0; x < TileWidth; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float d = Mathf.Abs(px - HalfW) / HalfW + Mathf.Abs(py - HalfH) / HalfH;
                    if (d > 1f) { pixels[y * TileWidth + x] = new Color32(0, 0, 0, 0); continue; }

                    float shade = 1f + (Hash(x, y) - 0.5f) * 0.08f;
                    switch (style)
                    {
                        case TileStyle.Grass:
                            if (Hash(x * 3 + 7, y * 5 + 1) > 0.93f) shade *= 0.85f; // grass tufts
                            break;
                        case TileStyle.Soil:
                            if (((x + y * 2) / 6) % 2 == 0) shade *= 0.9f; // furrows
                            break;
                        case TileStyle.Water:
                            float wave = Mathf.Sin(px * 0.25f + py * 0.6f);
                            if (wave > 0.85f) shade *= 1.18f;
                            break;
                        case TileStyle.Road:
                            if (d > 0.82f) shade *= 0.85f; // kerb
                            break;
                    }

                    if (d > 0.95f) shade *= 0.9f; // subtle cell edge so the grid reads in placement mode
                    pixels[y * TileWidth + x] = Scale(baseColor, shade);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private static Texture2D RenderCellHighlight()
        {
            var tex = NewTexture(TileWidth, TileHeight);
            var pixels = new Color32[TileWidth * TileHeight];
            for (int y = 0; y < TileHeight; y++)
            {
                for (int x = 0; x < TileWidth; x++)
                {
                    float d = Mathf.Abs(x + 0.5f - HalfW) / HalfW + Mathf.Abs(y + 0.5f - HalfH) / HalfH;
                    byte a = d > 0.94f ? (byte)0 : d > 0.84f ? (byte)255 : (byte)120;
                    pixels[y * TileWidth + x] = new Color32(255, 255, 255, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        // ------------------------------------------------------------------ Buildings

        /// <summary>
        /// Renders an isometric box: footprint diamond (FootprintX x FootprintY cells) extruded by HeightPx,
        /// with a darker roof cap, windows and a door. Light comes from the upper left (left face brighter).
        /// </summary>
        private static Texture2D RenderBuilding(BuildingArtSpec s)
        {
            int w = s.FootprintX, h = s.FootprintY;
            int texW = (w + h) * HalfW;
            int roofCap = 18;
            int texH = (w + h) * HalfH + s.HeightPx + roofCap + 2;

            var B = new Vector2(h * HalfW, 0);
            var R = B + new Vector2(w * HalfW, w * HalfH);
            var L = B + new Vector2(-h * HalfW, h * HalfH);
            var T = R + new Vector2(-h * HalfW, h * HalfH);
            var up = new Vector2(0, s.HeightPx);
            var cap = new Vector2(0, roofCap);

            var leftFace = new[] { L, B, B + up, L + up };
            var rightFace = new[] { B, R, R + up, B + up };
            var roofSideL = new[] { L + up, B + up, B + up + cap, L + up + cap };
            var roofSideR = new[] { B + up, R + up, R + up + cap, B + up + cap };
            var roofTop = new[] { L + up + cap, B + up + cap, R + up + cap, T + up + cap };

            var tex = NewTexture(texW, texH);
            var pixels = new Color32[texW * texH];
            var wallLit = s.Wall;
            var wallShade = Scale(s.Wall, 0.72f);
            var window = new Color32(70, 110, 150, 255);
            var door = new Color32(110, 70, 45, 255);

            for (int y = 0; y < texH; y++)
            {
                for (int x = 0; x < texW; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    Color32 c = new Color32(0, 0, 0, 0);

                    if (Inside(roofTop, p)) c = Scale(s.Roof, 1.1f);
                    else if (Inside(roofSideL, p)) c = s.Roof;
                    else if (Inside(roofSideR, p)) c = Scale(s.Roof, 0.75f);
                    else if (Inside(leftFace, p))
                    {
                        FaceUV(L, B - L, up, p, out float u, out float v);
                        c = IsWindow(u, v, w + h) ? Scale(window, 1.1f) : wallLit;
                    }
                    else if (Inside(rightFace, p))
                    {
                        FaceUV(B, R - B, up, p, out float u, out float v);
                        bool isDoor = u > 0.42f && u < 0.58f && v < 0.42f;
                        c = isDoor ? door : IsWindow(u, v, w + h) ? window : wallShade;
                    }

                    pixels[y * texW + x] = c;
                }
            }

            AddOutline(pixels, texW, texH);
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private static bool IsWindow(float u, float v, int span)
        {
            int columns = Mathf.Max(1, span);
            float cell = u * columns - Mathf.Floor(u * columns);
            return v > 0.5f && v < 0.78f && cell > 0.3f && cell < 0.7f;
        }

        /// <summary>Solves p = origin + edge * u + up * v.</summary>
        private static void FaceUV(Vector2 origin, Vector2 edge, Vector2 up, Vector2 p, out float u, out float v)
        {
            var d = p - origin;
            u = Mathf.Approximately(edge.x, 0f) ? 0f : d.x / edge.x;
            v = (d.y - edge.y * u) / up.y;
        }

        private static void AddOutline(Color32[] pixels, int w, int h)
        {
            var copy = (Color32[])pixels.Clone();
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (copy[i].a == 0) continue;
                    bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1 ||
                                copy[i - 1].a == 0 || copy[i + 1].a == 0 || copy[i - w].a == 0 || copy[i + w].a == 0;
                    if (edge) pixels[i] = Scale(copy[i], 0.45f);
                }
            }
        }

        // ------------------------------------------------------------------ Farming

        public const string CropsArtFolder = "Assets/_Game/Art/Crops";
        public const string FarmingBuildingsFolder = BuildingsArtFolder + "/Farming";
        private const int PlantHeadroom = 56; // extra pixels above the 1x1 diamond for tall plants

        public enum CropStyle { Wheat, Corn, Soybean, Sugarcane }

        /// <summary>1x1 field: tilled soil diamond with a slightly raised darker rim. Pivot = bottom corner (0.5, 0).</summary>
        public static Sprite CreateFieldSprite() =>
            CreateSprite($"{FarmingBuildingsFolder}/PH_Building_field.png", RenderField, new Vector2(0.5f, 0f), SpriteMeshType.FullRect);

        /// <summary>Four growth stages (sprout, growing, almost, ready). Same canvas and pivot as the field so they overlay exactly.</summary>
        public static Sprite[] CreateCropStages(string cropId, CropStyle style)
        {
            var sprites = new Sprite[4];
            for (int stage = 0; stage < 4; stage++)
            {
                int s = stage;
                sprites[stage] = CreateSprite($"{CropsArtFolder}/PH_Crop_{cropId}_{stage + 1}.png",
                    () => RenderCrop(style, s), new Vector2(0.5f, 0f), SpriteMeshType.FullRect);
            }

            return sprites;
        }

        private static Texture2D RenderField()
        {
            int w = TileWidth, h = TileHeight + 6;
            var tex = NewTexture(w, h);
            var pixels = new Color32[w * h];
            var soil = new Color32(150, 100, 60, 255);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // Diamond raised by 6 px: rim side faces below it.
                    float px = x + 0.5f, py = y + 0.5f;
                    float top = Mathf.Abs(px - HalfW) / HalfW + Mathf.Abs(py - 6 - HalfH) / HalfH;
                    float bottom = Mathf.Abs(px - HalfW) / HalfW + Mathf.Abs(py - HalfH) / HalfH;
                    Color32 c = new Color32(0, 0, 0, 0);
                    if (top <= 0.98f)
                    {
                        float shade = top > 0.86f ? 0.8f : (((x + 2 * y) / 7) % 2 == 0 ? 0.92f : 1.04f); // rim + furrows
                        c = Scale(soil, shade * (1f + (Hash(x, y) - 0.5f) * 0.06f));
                    }
                    else if (bottom <= 0.98f && py < HalfH + 6)
                    {
                        c = Scale(soil, px < HalfW ? 0.7f : 0.55f); // raised edge, lit left / shaded right
                    }

                    pixels[y * w + x] = c;
                }
            }

            AddOutline(pixels, w, h);
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private static Texture2D RenderCrop(CropStyle style, int stage)
        {
            int w = TileWidth, h = TileHeight + PlantHeadroom;
            var tex = NewTexture(w, h);
            var pixels = new Color32[w * h];

            float[] heights = style == CropStyle.Wheat ? new[] { 7f, 16f, 25f, 32f }
                : style == CropStyle.Soybean ? new[] { 5f, 10f, 15f, 18f }
                : style == CropStyle.Sugarcane ? new[] { 10f, 24f, 40f, 54f } : new[] { 9f, 22f, 36f, 46f };
            Color32 green = new Color32(70, 150, 50, 255);
            Color32 color = stage switch
            {
                0 => new Color32(110, 190, 70, 255),
                1 => green,
                2 => style == CropStyle.Wheat ? new Color32(160, 180, 60, 255) : new Color32(60, 140, 45, 255),
                _ => style == CropStyle.Wheat ? new Color32(230, 190, 70, 255) : new Color32(70, 150, 50, 255),
            };
            float plantHeight = heights[stage];

            // Plant positions on a lattice inside the diamond, drawn back (high y) to front (low y).
            var bases = new System.Collections.Generic.List<Vector2>();
            for (int gy = 0; gy < 7; gy++)
            for (int gx = 0; gx < 7; gx++)
            {
                // 0..1 along the two grid axes, shrunk around the centre so plants stay inside the rim.
                float u = 0.5f + ((gx + 0.5f) / 7f - 0.5f) * 0.8f, v = 0.5f + ((gy + 0.5f) / 7f - 0.5f) * 0.8f;
                var p = new Vector2(HalfW + (u - v) * HalfW, 6 + (u + v) * HalfH);
                bases.Add(p);
            }

            bases.Sort((a, b) => b.y.CompareTo(a.y));
            foreach (var b in bases)
            {
                float lean = (Hash((int)b.x, (int)b.y) - 0.5f) * 3f;
                float hgt = plantHeight * (0.85f + Hash((int)b.y, (int)b.x) * 0.3f);
                for (int i = 0; i < hgt; i++)
                {
                    int x = Mathf.RoundToInt(b.x + lean * i / Mathf.Max(1f, hgt));
                    int y = Mathf.RoundToInt(b.y + i);
                    var shade = Scale(color, 0.85f + 0.25f * i / Mathf.Max(1f, hgt));
                    Plot(pixels, w, h, x, y, shade);
                    Plot(pixels, w, h, x + 1, y, Scale(shade, 0.85f));
                }

                // Wheat ears / corn cobs on the last two stages.
                if (stage == 3 && style == CropStyle.Wheat)
                    FillEllipse(pixels, w, h, b.x + lean, b.y + hgt - 2, 2.2f, 5f, new Color32(245, 210, 95, 255));
                if (stage >= 1 && style == CropStyle.Soybean)
                {
                    // Low leafy bush; tan pods when ripe.
                    FillEllipse(pixels, w, h, b.x + lean, b.y + hgt, 3.5f + stage, 2.5f + stage * 0.6f, Scale(color, 0.95f));
                    if (stage == 3)
                        FillEllipse(pixels, w, h, b.x + lean + 2, b.y + hgt - 1, 1.6f, 3f, new Color32(214, 192, 120, 255));
                }

                if (stage >= 1 && style == CropStyle.Sugarcane)
                {
                    // Darker joints along the cane and a leaf tuft on top.
                    for (int j = 8; j < hgt; j += 9)
                        Plot(pixels, w, h, Mathf.RoundToInt(b.x + lean * j / Mathf.Max(1f, hgt)), Mathf.RoundToInt(b.y + j), Scale(color, 0.6f));
                    FillEllipse(pixels, w, h, b.x + lean, b.y + hgt, 3f + stage, 1.6f, new Color32(110, 180, 70, 255));
                }

                if (stage >= 2 && style == CropStyle.Corn)
                    FillEllipse(pixels, w, h, b.x + lean * 0.6f + 2, b.y + hgt * 0.6f, 2f, stage == 3 ? 5f : 3f,
                        stage == 3 ? new Color32(250, 210, 60, 255) : new Color32(150, 200, 90, 255));
            }

            AddOutline(pixels, w, h);
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private static void Plot(Color32[] pixels, int w, int h, int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            pixels[y * w + x] = c;
        }

        private static void FillEllipse(Color32[] pixels, int w, int h, float cx, float cy, float rx, float ry, Color32 c)
        {
            for (int y = Mathf.FloorToInt(cy - ry); y <= Mathf.CeilToInt(cy + ry); y++)
            for (int x = Mathf.FloorToInt(cx - rx); x <= Mathf.CeilToInt(cx + rx); x++)
            {
                float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                if (dx * dx + dy * dy <= 1f) Plot(pixels, w, h, x, y, c);
            }
        }

        // ------------------------------------------------------------------ Icons

        public const string ConstructionIconPath = UiArtFolder + "/PH_Icon_Construction.png";

        /// <summary>Hammer in a round badge, floated above buildings under construction. 128x128, pivot centre.</summary>
        public enum DecorationStyle { Tree, FlowerBed, Bench, Fence, Fountain, Scarecrow, Lantern }

        public const string DecorationsArtFolder = BuildingsArtFolder + "/Decorations";

        /// <summary>Decoration sprite on a w×h footprint (canvas width (w+h)×64, pivot at the bottom corner, like buildings).</summary>
        public static Sprite CreateDecoration(string id, DecorationStyle style, int w, int h)
        {
            var pivot = new Vector2((float)h / (w + h), 0f);
            return CreateSprite($"{DecorationsArtFolder}/PH_Building_{id}.png", () => RenderDecoration(style, w, h), pivot, SpriteMeshType.Tight);
        }

        private static Texture2D RenderDecoration(DecorationStyle style, int w, int h)
        {
            int texW = (w + h) * HalfW, texH = (w + h) * HalfH + 150;
            var tex = NewTexture(texW, texH);
            var pixels = new Color32[texW * texH];
            var B = new Vector2(h * HalfW, 0f);
            Vector2 P(float gx, float gy) => B + new Vector2(gx * HalfW - gy * HalfW, gx * HalfH + gy * HalfH);
            var centre = P(w * 0.5f, h * 0.5f);

            void Ellipse(Vector2 c, float rx, float ry, Color32 col, bool shade = true)
            {
                for (int y = Mathf.Max(0, (int)(c.y - ry)); y <= Mathf.Min(texH - 1, (int)(c.y + ry)); y++)
                for (int x = Mathf.Max(0, (int)(c.x - rx)); x <= Mathf.Min(texW - 1, (int)(c.x + rx)); x++)
                {
                    float dx = (x + 0.5f - c.x) / rx, dy = (y + 0.5f - c.y) / ry;
                    if (dx * dx + dy * dy > 1f) continue;
                    pixels[y * texW + x] = shade ? Scale(col, 0.9f + 0.2f * Mathf.Clamp01(dy * 0.5f + 0.5f - dx * 0.3f)) : col;
                }
            }

            void Rect(float x0, float y0, float x1, float y1, Color32 col)
            {
                for (int y = Mathf.Max(0, (int)y0); y < Mathf.Min(texH, (int)y1); y++)
                for (int x = Mathf.Max(0, (int)x0); x < Mathf.Min(texW, (int)x1); x++)
                    pixels[y * texW + x] = col;
            }

            switch (style)
            {
                case DecorationStyle.Tree:
                    Ellipse(centre + new Vector2(0, 2), 26, 10, new Color32(70, 120, 50, 255), false);            // shadow-ish base
                    Rect(centre.x - 5, centre.y, centre.x + 5, centre.y + 40, new Color32(130, 86, 52, 255));   // trunk
                    Ellipse(centre + new Vector2(0, 62), 36, 32, new Color32(70, 160, 70, 255));
                    Ellipse(centre + new Vector2(-14, 80), 24, 22, new Color32(84, 178, 78, 255));
                    Ellipse(centre + new Vector2(14, 84), 22, 20, new Color32(96, 190, 86, 255));
                    break;
                case DecorationStyle.FlowerBed:
                    for (int y = 0; y < texH; y++)
                    for (int x = 0; x < texW; x++)
                    {
                        var p = new Vector2(x + 0.5f, y + 0.5f);
                        if (Inside(new[] { P(0.1f, 0.9f), P(0.1f, 0.1f), P(0.9f, 0.1f), P(0.9f, 0.9f) }, p) ||
                            Inside(new[] { P(0.1f, 0.1f), P(0.9f, 0.1f), P(0.9f, 0.1f) + new Vector2(0, 8), P(0.1f, 0.1f) + new Vector2(0, 8) }, p))
                            pixels[y * texW + x] = new Color32(150, 104, 66, 255);
                    }

                    var flowerColours = new[] { new Color32(240, 80, 110, 255), new Color32(255, 210, 60, 255), new Color32(170, 110, 230, 255), new Color32(255, 140, 60, 255) };
                    for (int i = 0; i < 4; i++)
                    for (int j = 0; j < 4; j++)
                    {
                        var c = P(0.2f + i * 0.2f, 0.2f + j * 0.2f) + new Vector2(0, 12);
                        Ellipse(c + new Vector2(0, -4), 3, 6, new Color32(60, 140, 60, 255), false);
                        Ellipse(c + new Vector2(0, 4), 6, 5, flowerColours[(i + j) % 4]);
                    }

                    break;
                case DecorationStyle.Bench:
                {
                    var wood = new Color32(176, 120, 66, 255);
                    var a = P(0.2f, 0.5f); var b = P(0.8f, 0.5f);
                    for (float t = 0; t <= 1f; t += 0.01f)
                    {
                        var p = Vector2.Lerp(a, b, t);
                        Rect(p.x - 2, p.y + 14, p.x + 2, p.y + 22, wood);                                    // seat
                        Rect(p.x - 2, p.y + 26, p.x + 2, p.y + 40, Scale(wood, 0.85f));                     // back
                    }

                    foreach (var leg in new[] { 0.25f, 0.75f })
                    {
                        var p = Vector2.Lerp(a, b, leg);
                        Rect(p.x - 3, p.y, p.x + 3, p.y + 16, new Color32(60, 60, 66, 255));
                    }

                    break;
                }
                case DecorationStyle.Fence:
                {
                    var wood = new Color32(236, 226, 206, 255);
                    var a = P(0.05f, 0.5f); var b = P(0.95f, 0.5f);
                    for (float t = 0; t <= 1f; t += 0.01f)
                    {
                        var p = Vector2.Lerp(a, b, t);
                        Rect(p.x - 2, p.y + 12, p.x + 2, p.y + 16, Scale(wood, 0.85f));
                        Rect(p.x - 2, p.y + 26, p.x + 2, p.y + 30, Scale(wood, 0.85f));
                    }

                    foreach (var post in new[] { 0.05f, 0.35f, 0.65f, 0.95f })
                    {
                        var p = Vector2.Lerp(a, b, post);
                        Rect(p.x - 4, p.y, p.x + 4, p.y + 38, wood);
                    }

                    break;
                }
                case DecorationStyle.Fountain:
                    Ellipse(centre, 100, 50, new Color32(170, 170, 176, 255), false);                       // rim
                    Ellipse(centre + new Vector2(0, 6), 100, 50, new Color32(190, 190, 196, 255), false);
                    Ellipse(centre + new Vector2(0, 8), 84, 40, new Color32(80, 160, 220, 255));             // water
                    Rect(centre.x - 9, centre.y + 8, centre.x + 9, centre.y + 70, new Color32(186, 186, 192, 255)); // column
                    Ellipse(centre + new Vector2(0, 72), 26, 10, new Color32(176, 176, 182, 255), false);   // top bowl
                    Ellipse(centre + new Vector2(0, 92), 8, 18, new Color32(160, 215, 250, 220));            // water jet
                    break;
                case DecorationStyle.Scarecrow:
                {
                    var wood = new Color32(140, 96, 56, 255);
                    Rect(centre.x - 4, centre.y, centre.x + 4, centre.y + 110, wood);                        // pole
                    Rect(centre.x - 40, centre.y + 70, centre.x + 40, centre.y + 77, wood);                  // arms
                    Rect(centre.x - 22, centre.y + 40, centre.x + 22, centre.y + 82, new Color32(70, 120, 190, 255)); // shirt
                    Rect(centre.x - 44, centre.y + 66, centre.x - 34, centre.y + 80, new Color32(230, 200, 90, 255)); // straw hands
                    Rect(centre.x + 34, centre.y + 66, centre.x + 44, centre.y + 80, new Color32(230, 200, 90, 255));
                    Ellipse(centre + new Vector2(0, 96), 15, 15, new Color32(236, 206, 150, 255));          // sack head
                    Ellipse(centre + new Vector2(0, 112), 26, 6, new Color32(200, 150, 60, 255), false);    // hat brim
                    Rect(centre.x - 13, centre.y + 112, centre.x + 13, centre.y + 128, new Color32(190, 140, 50, 255)); // hat
                    Rect(centre.x - 13, centre.y + 114, centre.x + 13, centre.y + 118, new Color32(200, 60, 50, 255));  // hat band
                    break;
                }
                case DecorationStyle.Lantern:
                {
                    var iron = new Color32(60, 62, 70, 255);
                    Ellipse(centre + new Vector2(0, 4), 16, 7, new Color32(80, 82, 90, 255), false);        // foot
                    Rect(centre.x - 3, centre.y, centre.x + 3, centre.y + 100, iron);                        // post
                    Rect(centre.x, centre.y + 96, centre.x + 30, centre.y + 101, iron);                      // arm
                    Rect(centre.x + 26, centre.y + 72, centre.x + 28, centre.y + 98, iron);                  // hook
                    Ellipse(centre + new Vector2(27, 62), 14, 18, new Color32(255, 196, 70, 255));          // paper lantern
                    Ellipse(centre + new Vector2(27, 62), 8, 12, new Color32(255, 236, 150, 255));          // glow
                    Rect(centre.x + 17, centre.y + 77, centre.x + 37, centre.y + 81, new Color32(200, 60, 50, 255)); // cap
                    Rect(centre.x + 17, centre.y + 43, centre.x + 37, centre.y + 47, new Color32(200, 60, 50, 255)); // bottom
                    break;
                }
            }

            AddOutline(pixels, texW, texH);
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        public const string CoinIconPath = UiArtFolder + "/PH_Icon_Coin.png";
        public const string GemIconPath = UiArtFolder + "/PH_Icon_Gem.png";
        public const string XpIconPath = UiArtFolder + "/PH_Icon_Xp.png";

        /// <summary>Coin, gem and XP-star icons (rewards flying to the top bar).</summary>
        public static readonly string TruckIconPath = UiArtFolder + "/PH_Icon_Truck.png";

        /// <summary>Delivery truck icon (badge above the depot): a green cab and a cream cargo box, side-on, 128x128.</summary>
        public static Sprite CreateTruckIcon() =>
            CreateSprite(TruckIconPath, RenderTruckIcon, new Vector2(0.5f, 0.5f), SpriteMeshType.FullRect);

        private static Texture2D RenderTruckIcon()
        {
            const int size = 128;
            var tex = NewTexture(size, size);
            var px = new Color32[size * size];
            void Box(float x0, float y0, float x1, float y1, Color32 c)
            {
                for (int y = Mathf.Max(0, (int)y0); y < Mathf.Min(size, (int)y1); y++)
                for (int x = Mathf.Max(0, (int)x0); x < Mathf.Min(size, (int)x1); x++)
                    px[y * size + x] = c;
            }

            void Wheel(float cx, float cy)
            {
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    if (d <= 13f) px[y * size + x] = d <= 6f ? new Color32(200, 200, 206, 255) : new Color32(46, 46, 52, 255);
                }
            }

            Box(10, 34, 82, 100, new Color32(244, 232, 204, 255));   // cargo box
            Box(10, 34, 82, 42, new Color32(214, 196, 160, 255));    // box floor
            Box(18, 60, 74, 74, new Color32(232, 120, 60, 255));     // stripe on the box
            Box(84, 34, 118, 80, new Color32(70, 160, 80, 255));     // cab
            Box(88, 56, 112, 76, new Color32(170, 220, 245, 255));   // window
            Box(112, 38, 120, 48, new Color32(255, 220, 90, 255));   // headlight
            Wheel(32, 30);
            Wheel(98, 30);
            AddOutline(px, size, size);
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        public static void CreateRewardIcons()
        {
            CreateSprite(CoinIconPath, () => RenderRewardIcon(0), new Vector2(0.5f, 0.5f), SpriteMeshType.FullRect);
            CreateSprite(GemIconPath, () => RenderRewardIcon(1), new Vector2(0.5f, 0.5f), SpriteMeshType.FullRect);
            CreateSprite(XpIconPath, () => RenderRewardIcon(2), new Vector2(0.5f, 0.5f), SpriteMeshType.FullRect);
        }

        private static Texture2D RenderRewardIcon(int kind)
        {
            const int size = 64;
            var tex = NewTexture(size, size);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f - 32f, py = y + 0.5f - 32f;
                Color32? c = null;
                if (kind == 0)
                {
                    float r = Mathf.Sqrt(px * px + py * py);
                    if (r < 27f) c = r > 20f ? new Color32(222, 160, 30, 255) : Scale(new Color32(250, 204, 60, 255), py > 0 ? 1.08f : 0.95f);
                    if (r < 12f && Mathf.Abs(px) < 3f) c = new Color32(222, 160, 30, 255); // stamp
                }
                else if (kind == 1)
                {
                    // Gem: diamond with a lighter top facet.
                    if (Mathf.Abs(px) / 26f + Mathf.Abs(py - 2f) / 28f < 1f)
                        c = py > 8f ? new Color32(150, 230, 250, 255) : px < 0 ? new Color32(60, 170, 230, 255) : new Color32(40, 130, 200, 255);
                }
                else
                {
                    // XP: five-pointed star, one point straight up. Radius goes from the outer point to the inner notch
                    // within each 72-degree sector.
                    float angle = Mathf.Atan2(px, py + 2f);
                    float sector = 2f * Mathf.PI / 5f;
                    float phi = Mathf.Repeat(angle + sector * 0.5f, sector) - sector * 0.5f;
                    float star = Mathf.Lerp(29f, 12f, Mathf.Abs(phi) / (sector * 0.5f));
                    float rr = Mathf.Sqrt(px * px + (py + 2f) * (py + 2f));
                    if (rr < star) c = Scale(new Color32(110, 200, 255, 255), py > 0 ? 1.1f : 0.92f);
                }

                if (c.HasValue) pixels[y * size + x] = c.Value;
            }

            AddOutline(pixels, size, size);
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        public const string CharactersArtFolder = "Assets/_Game/Art/Characters";
        public const string VehiclesArtFolder = "Assets/_Game/Art/Vehicles";
        public static string PersonSpritePath(int i) => $"{CharactersArtFolder}/PH_Person_{i}.png";
        public static readonly string CarFrontPath = VehiclesArtFolder + "/PH_Vehicle_car_front.png";
        public static readonly string CarBackPath = VehiclesArtFolder + "/PH_Vehicle_car_back.png";
        public const int PersonVariants = 4;

        /// <summary>Four simple townsfolk (front view, pivot at the feet).</summary>
        public static Sprite[] CreatePeopleSprites()
        {
            var shirts = new[] { new Color32(220, 80, 70, 255), new Color32(70, 130, 210, 255), new Color32(240, 190, 60, 255), new Color32(90, 170, 90, 255) };
            var hair = new[] { new Color32(90, 60, 40, 255), new Color32(40, 36, 34, 255), new Color32(200, 150, 70, 255), new Color32(120, 70, 40, 255) };
            var sprites = new Sprite[PersonVariants];
            for (int i = 0; i < PersonVariants; i++)
            {
                int v = i;
                sprites[i] = CreateSprite(PersonSpritePath(i + 1), () => RenderPerson(shirts[v], hair[v], v % 2 == 1), new Vector2(0.5f, 0f), SpriteMeshType.FullRect);
            }

            return sprites;
        }

        private static Texture2D RenderPerson(Color32 shirt, Color32 hairColor, bool skirt)
        {
            const int w = 30, h = 62;
            var tex = NewTexture(w, h);
            var pixels = new Color32[w * h];
            var skin = new Color32(244, 200, 160, 255);
            var trousers = new Color32(70, 80, 110, 255);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float px = x + 0.5f - 15f, py = y + 0.5f;
                Color32? c = null;
                if (py < 20f && (Mathf.Abs(px + 4f) < 3f || Mathf.Abs(px - 4f) < 3f)) c = py < 4f ? new Color32(60, 46, 40, 255) : trousers; // legs + shoes
                if (skirt && py >= 12f && py < 22f && Mathf.Abs(px) < 8f + (22f - py) * 0.35f) c = Scale(shirt, 0.8f);
                if (py >= 18f && py < 40f && Mathf.Abs(px) < 8.5f) c = Scale(shirt, px < 0 ? 1.08f : 0.9f);                  // body
                if (py >= 20f && py < 36f && Mathf.Abs(Mathf.Abs(px) - 10.5f) < 2.2f) c = Scale(shirt, 0.85f);               // arms
                float hx = px, hy = py - 48f;
                if (hx * hx + hy * hy < 64f) c = skin;                                                                        // head
                if (hx * hx + hy * hy < 72f && hy > 2f) c = hairColor;                                                       // hair
                if (c.HasValue) pixels[y * w + x] = c.Value;
            }

            AddOutline(pixels, w, h);
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>Small car along the grid X axis (1.3 × 0.7 cells), seen from the front (driving towards the viewer) or the back.</summary>
        public static Sprite CreateCarSprite(bool front) =>
            CreateSprite(front ? CarFrontPath : CarBackPath, () => RenderCar(front), new Vector2(0.35f, 0f), SpriteMeshType.Tight);

        private static Texture2D RenderCar(bool front)
        {
            const int texW = 128, texH = 100;
            var tex = NewTexture(texW, texH);
            var pixels = new Color32[texW * texH];
            var B = new Vector2(0.7f * HalfW, 0f);
            Vector2 P(float gx, float gy) => B + new Vector2(gx * HalfW - gy * HalfW, gx * HalfH + gy * HalfH);
            var body = new Color32(60, 150, 220, 255);

            void Box(float x0, float x1, float y0, float y1, float z0, float z1, Color32 col, bool glassSides)
            {
                var b = P(x0, y0); var r = P(x1, y0); var l = P(x0, y1); var t = P(x1, y1);
                var lo = new Vector2(0, z0); var hi = new Vector2(0, z1);
                var leftFace = new[] { l + lo, b + lo, b + hi, l + hi };
                var rightFace = new[] { b + lo, r + lo, r + hi, b + hi };
                var top = new[] { l + hi, b + hi, r + hi, t + hi };
                var glass = new Color32(170, 215, 240, 255);
                for (int y = 0; y < texH; y++)
                for (int x = 0; x < texW; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    if (Inside(top, p)) pixels[y * texW + x] = Scale(col, 1.15f);
                    else if (Inside(leftFace, p)) pixels[y * texW + x] = glassSides ? glass : col;
                    else if (Inside(rightFace, p)) pixels[y * texW + x] = glassSides ? Scale(glass, 0.85f) : Scale(col, 0.75f);
                }
            }

            Box(0.05f, 1.25f, 0.06f, 0.64f, 0f, 6f, new Color32(40, 40, 46, 255), false); // wheels / underside
            Box(0f, 1.3f, 0f, 0.7f, 5f, 22f, body, false);                                  // body
            Box(0.3f, 1.0f, 0.1f, 0.6f, 22f, 34f, body, true);                              // cabin with windows

            // Lights on the visible end (the face at x = 0): headlights at the front, tail lights at the back.
            var lights = front ? new Color32(255, 236, 140, 255) : new Color32(220, 40, 40, 255);
            foreach (float gy in new[] { 0.15f, 0.55f })
            {
                var c = P(0f, gy) + new Vector2(0, 14f);
                for (int y = (int)c.y - 2; y <= (int)c.y + 2; y++)
                for (int x = (int)c.x - 2; x <= (int)c.x + 2; x++)
                    if (x >= 0 && y >= 0 && x < texW && y < texH) pixels[y * texW + x] = lights;
            }

            AddOutline(pixels, texW, texH);
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        public const string TrainSpritePath = "Assets/_Game/Art/Vehicles/PH_Vehicle_train.png";
        public const int TrainLengthCells = 5;

        /// <summary>
        /// Placeholder train on a 1×5-cell footprint along the grid's Y axis (screen up-left = the direction it drives):
        /// green and blue wagons, red locomotive in front. Pivot = bottom corner of the footprint.
        /// </summary>
        public static Sprite CreateTrainSprite() =>
            CreateSprite(TrainSpritePath, RenderTrain, new Vector2(TrainLengthCells / (float)(1 + TrainLengthCells), 0f), SpriteMeshType.Tight);

        private static Texture2D RenderTrain()
        {
            int len = TrainLengthCells;
            int texW = (1 + len) * HalfW, texH = (1 + len) * HalfH + 80;
            var tex = NewTexture(texW, texH);
            var pixels = new Color32[texW * texH];
            var B = new Vector2(len * HalfW, 0f);
            Vector2 P(float gx, float gy) => B + new Vector2(gx * HalfW - gy * HalfW, gx * HalfH + gy * HalfH);

            // (start y, end y, height, colour) from the rear; drawn back (high y) to front so nearer cars overlap.
            var cars = new[]
            {
                (y0: 3.3f, y1: 4.85f, h: 44f, c: new Color32(206, 56, 46, 255)),  // locomotive (front)
                (y0: 1.7f, y1: 3.1f, h: 34f, c: new Color32(70, 120, 200, 255)),   // wagon
                (y0: 0.15f, y1: 1.5f, h: 34f, c: new Color32(86, 168, 72, 255)),   // wagon (rear)
            };

            foreach (var car in cars)
            {
                float x0 = 0.18f, x1 = 0.82f;
                var b = P(x0, car.y0); var r = P(x1, car.y0); var l = P(x0, car.y1); var t = P(x1, car.y1);
                var wheels = new Vector2(0, 8f);
                var up = new Vector2(0, car.h);
                var leftFace = new[] { l + wheels, b + wheels, b + up, l + up };
                var rightFace = new[] { b + wheels, r + wheels, r + up, b + up };
                var top = new[] { l + up, b + up, r + up, t + up };
                var under = new[] { l, b, b + wheels, l + wheels };
                var underR = new[] { b, r, r + wheels, b + wheels };
                for (int y = 0; y < texH; y++)
                for (int x = 0; x < texW; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    Color32? col = null;
                    if (Inside(top, p)) col = Scale(car.c, car.h > 40f ? 0.55f : 1.15f); // loco: dark roof; wagons: open load
                    else if (Inside(leftFace, p)) col = car.c;
                    else if (Inside(rightFace, p)) col = Scale(car.c, 0.72f);
                    else if (Inside(under, p) || Inside(underR, p)) col = new Color32(48, 48, 54, 255); // wheels
                    if (col.HasValue) pixels[y * texW + x] = col.Value;
                }

                if (car.h > 40f)
                {
                    // Chimney near the front of the locomotive.
                    var c0 = P(0.5f, car.y1 - 0.35f) + up;
                    for (int y = (int)c0.y; y < (int)c0.y + 22 && y < texH; y++)
                    for (int x = (int)c0.x - 6; x <= (int)c0.x + 6; x++)
                        if (x >= 0 && x < texW) pixels[y * texW + x] = Scale(new Color32(60, 60, 66, 255), x < c0.x ? 1.15f : 0.85f);
                }
                else
                {
                    // Wooden crates on the wagon.
                    var c0 = P(0.5f, (car.y0 + car.y1) * 0.5f) + up;
                    for (int y = (int)c0.y; y < (int)c0.y + 16 && y < texH; y++)
                    for (int x = (int)c0.x - 14; x <= (int)c0.x + 14; x++)
                        if (x >= 0 && x < texW) pixels[y * texW + x] = Scale(new Color32(196, 140, 80, 255), (x - (int)c0.x) % 14 == 0 ? 0.7f : 1f);
                }
            }

            AddOutline(pixels, texW, texH);
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        public const string SmokePuffPath = "Assets/_Game/Art/Effects/PH_Fx_Smoke.png";

        /// <summary>Soft round white puff (tinted and faded by ChimneySmoke).</summary>
        public static Sprite CreateSmokePuff() =>
            CreateSprite(SmokePuffPath, RenderSmokePuff, new Vector2(0.5f, 0.5f), SpriteMeshType.FullRect);

        private static Texture2D RenderSmokePuff()
        {
            const int size = 64;
            var tex = NewTexture(size, size);
            var pixels = new Color32[size * size];
            // A few overlapping soft blobs, so the puff looks cloudy rather than a perfect circle.
            var blobs = new[] { new Vector3(32f, 30f, 20f), new Vector3(22f, 36f, 13f), new Vector3(42f, 37f, 12f), new Vector3(33f, 42f, 13f) };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float a = 0f;
                foreach (var b in blobs)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(b.x, b.y)) / b.z;
                    a = Mathf.Max(a, Mathf.Clamp01(1f - d * d));
                }

                byte shade = (byte)(232 + 23 * Mathf.Clamp01((y - 20f) / 30f));
                pixels[y * size + x] = new Color32(shade, shade, shade, (byte)(a * 255));
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        public static Sprite CreateConstructionIcon() =>
            CreateSprite(ConstructionIconPath, RenderConstructionIcon, new Vector2(0.5f, 0.5f), SpriteMeshType.FullRect);

        private static Texture2D RenderConstructionIcon()
        {
            const int size = 128;
            var tex = NewTexture(size, size);
            var pixels = new Color32[size * size];
            var badge = new Color32(250, 200, 60, 255);
            var handle = new Color32(140, 90, 50, 255);
            var head = new Color32(110, 115, 125, 255);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - 64f, dy = y + 0.5f - 64f;
                if (dx * dx + dy * dy > 60f * 60f) continue;
                var c = badge;

                // Rotate into hammer space (45 degrees).
                float u = (dx + dy) * 0.7071f, v = (dy - dx) * 0.7071f;
                if (Mathf.Abs(v) < 6f && u > -40f && u < 22f) c = handle;           // handle
                if (u > 14f && u < 34f && Mathf.Abs(v) < 26f) c = head;             // head
                pixels[y * size + x] = c;
            }

            AddOutline(pixels, size, size);
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        // ------------------------------------------------------------------ Products

        public const string ProductsArtFolder = "Assets/_Game/Art/Products";

        public enum ProductStyle { Bread, CornBread, ChickenFeed, CowFeed, Egg, Milk, Sugar, Cookies, Cake, Butter, Cheese, Planks, Bricks, Nails,
            Apple, Cherry, AppleJuice, CherryJuice }

        public const string MaterialsArtFolder = "Assets/_Game/Art/Materials";

        /// <summary>128x128 building-material icon (Planks, Bricks, Nails). Pivot centre.</summary>
        public static Sprite CreateMaterialIcon(string materialId, ProductStyle style) =>
            CreateSprite($"{MaterialsArtFolder}/PH_Material_{materialId}.png", () => RenderProduct(style), new Vector2(0.5f, 0.5f), SpriteMeshType.FullRect);

        /// <summary>128x128 product icon (UI and the "ready" bubble above factories). Pivot centre.</summary>
        public static Sprite CreateProductIcon(string productId, ProductStyle style) =>
            CreateSprite($"{ProductsArtFolder}/PH_Product_{productId}.png", () => RenderProduct(style), new Vector2(0.5f, 0.5f), SpriteMeshType.FullRect);

        private static Texture2D RenderProduct(ProductStyle style)
        {
            const int size = 128;
            var tex = NewTexture(size, size);
            var pixels = new Color32[size * size];

            if (style >= ProductStyle.Apple)
            {
                RenderOrchardIcon(pixels, size, style);
                AddOutline(pixels, size, size);
                tex.SetPixels32(pixels);
                tex.Apply();
                return tex;
            }

            if (style == ProductStyle.Planks)
            {
                // Three stacked wooden planks with grain lines.
                var wood = new Color32(196, 140, 80, 255);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    int plank = (int)((py - 24f) / 26f);
                    if (py < 24f || plank > 2 || px < 14f + plank * 6f || px > 114f - plank * 6f) continue;
                    float inPlank = (py - 24f) % 26f;
                    if (inPlank > 22f) continue; // gap
                    var c = Scale(wood, 0.85f + 0.06f * plank + (inPlank > 16f ? -0.12f : 0f));
                    if (((int)(px * 0.5f + py * 0.2f + plank * 7)) % 9 == 0) c = Scale(c, 0.82f);
                    pixels[y * size + x] = c;
                }
            }
            else if (style == ProductStyle.Bricks)
            {
                // Two rows of red bricks with light mortar.
                var brick = new Color32(192, 76, 56, 255);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    if (px < 12f || px > 116f || py < 28f || py > 96f) continue;
                    int row = (int)((py - 28f) / 34f);
                    float inRow = (py - 28f) % 34f;
                    float offset = row % 2 == 0 ? 0f : 26f;
                    float inBrick = (px - 12f + offset) % 52f;
                    bool mortar = inRow > 30f || inBrick < 4f;
                    var c = mortar ? new Color32(228, 214, 190, 255) : Scale(brick, inRow < 8f ? 1.1f : 0.95f);
                    pixels[y * size + x] = c;
                }
            }
            else if (style == ProductStyle.Nails)
            {
                // Three grey nails: flat heads on top, pointed shafts.
                var steel = new Color32(150, 156, 166, 255);
                var xs = new[] { 40f, 64f, 88f };
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                foreach (var nx in xs)
                {
                    float px = x + 0.5f - nx, py = y + 0.5f;
                    bool head = py > 98f && py < 108f && Mathf.Abs(px) < 12f;
                    float shaftHalf = py > 30f ? 4f : Mathf.Lerp(0f, 4f, (py - 16f) / 14f);
                    bool shaft = py >= 16f && py <= 98f && Mathf.Abs(px) < shaftHalf;
                    if (!head && !shaft) continue;
                    pixels[y * size + x] = head ? Scale(steel, 1.12f) : Scale(steel, px < 0 ? 1.05f : 0.85f);
                }
            }
            else if (style == ProductStyle.Cookies)
            {
                // Two round golden cookies with chocolate chips, overlapping.
                var dough = new Color32(214, 160, 90, 255);
                var centres = new[] { new Vector2(50f, 54f), new Vector2(80f, 74f) };
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                foreach (var c0 in centres)
                {
                    float dx = x + 0.5f - c0.x, dy = y + 0.5f - c0.y;
                    float d = (dx * dx + dy * dy) / (34f * 34f);
                    if (d > 1f) continue;
                    var c = Scale(dough, 0.9f + 0.15f * (1f - d));
                    if (Hash(x / 6, y / 6) > 0.86f && d < 0.8f) c = new Color32(90, 55, 35, 255);
                    pixels[y * size + x] = c;
                }
            }
            else if (style == ProductStyle.Cake)
            {
                // Round two-layer cake: sponge, cream band, pink icing on top, a red cherry.
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = Mathf.Abs(x + 0.5f - 64f), py = y + 0.5f;
                    bool body = px < 48f && py > 18f && py < 88f;
                    float topDy = (py - 88f) / 12f, topDx = px / 48f;
                    bool top = topDx * topDx + topDy * topDy <= 1f;
                    bool cherry = (x + 0.5f - 64f) * (x + 0.5f - 64f) + (py - 106f) * (py - 106f) < 64f;
                    if (!body && !top && !cherry) continue;
                    Color32 c = cherry ? new Color32(210, 40, 50, 255)
                        : top || py > 76f ? new Color32(246, 170, 196, 255)
                        : py > 48f && py < 56f ? new Color32(255, 246, 230, 255)
                        : new Color32(226, 180, 110, 255);
                    pixels[y * size + x] = c;
                }
            }
            else if (style == ProductStyle.Butter)
            {
                // Yellow block of butter on a small blue dish.
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = Mathf.Abs(x + 0.5f - 64f), py = y + 0.5f;
                    bool dish = px < 54f && py > 22f && py < 34f;
                    bool block = px < 38f && py >= 34f && py < 78f;
                    if (!dish && !block) continue;
                    pixels[y * size + x] = dish ? new Color32(100, 150, 210, 255)
                        : py > 70f ? new Color32(255, 236, 140, 255) : new Color32(246, 214, 100, 255);
                }
            }
            else if (style == ProductStyle.Cheese)
            {
                // Wedge of cheese with holes.
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    bool wedge = py > 26f && py < 90f && px > 18f && px < 110f && py - 26f < (110f - px) * 0.75f;
                    if (!wedge) continue;
                    var c = new Color32(250, 200, 70, 255);
                    if (Hash(x / 12, y / 12) > 0.75f && ((x % 12) - 6) * ((x % 12) - 6) + ((y % 12) - 6) * ((y % 12) - 6) < 14)
                        c = new Color32(214, 160, 50, 255);
                    pixels[y * size + x] = c;
                }
            }
            else if (style == ProductStyle.Sugar)
            {
                // Three white sugar cubes: two at the bottom, one on top.
                var cube = new Color32(250, 250, 252, 255);
                var cubes = new[] { new Vector2(42f, 34f), new Vector2(86f, 34f), new Vector2(64f, 76f) };
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                foreach (var c0 in cubes)
                {
                    float dx = x + 0.5f - c0.x, dy = y + 0.5f - c0.y;
                    if (Mathf.Abs(dx) > 21f || Mathf.Abs(dy) > 21f) continue;
                    var c = dy > 12f ? cube : dx > 12f ? Scale(cube, 0.82f) : Scale(cube, 0.92f);
                    if (((x * 7 + y * 13) % 11) == 0) c = Scale(c, 0.9f);
                    pixels[y * size + x] = c;
                }
            }
            else if (style == ProductStyle.Egg)
            {
                // Cream egg, a little wider at the bottom, with a soft highlight.
                var shell = new Color32(244, 232, 210, 255);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - 64f) / 34f, py = y + 0.5f - 60f;
                    float dy = py > 0f ? py / 50f : py / 40f;
                    float d = dx * dx + dy * dy;
                    if (d > 1f) continue;
                    var c = Scale(shell, 0.86f + 0.18f * (1f - d));
                    if ((x - 52f) * (x - 52f) + (y - 82f) * (y - 82f) < 60f) c = new Color32(255, 252, 245, 255);
                    pixels[y * size + x] = c;
                }
            }
            else if (style == ProductStyle.Milk)
            {
                // Milk bottle: white body, narrow neck, blue cap.
                var glass = new Color32(250, 250, 248, 255);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = Mathf.Abs(x + 0.5f - 64f), py = y + 0.5f;
                    float half = py < 74f ? 30f : py < 92f ? Mathf.Lerp(30f, 14f, (py - 74f) / 18f) : 14f;
                    bool body = py > 10f && py < 112f && px < half;
                    bool cap = py >= 104f && py < 120f && px < 16f;
                    if (!body && !cap) continue;
                    var c = cap ? new Color32(70, 130, 210, 255) : Scale(glass, 0.86f + 0.14f * (1f - px / 30f));
                    if (!cap && py > 30f && py < 56f) c = new Color32(120, 170, 230, 255);
                    pixels[y * size + x] = c;
                }
            }
            else if (style == ProductStyle.ChickenFeed || style == ProductStyle.CowFeed)
            {
                // Burlap feed sack, tied at the top, with a coloured band (yellow = chicken, green = cow).
                var sack = new Color32(205, 178, 128, 255);
                var band = style == ProductStyle.ChickenFeed ? new Color32(240, 196, 60, 255) : new Color32(96, 160, 80, 255);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f - 64f, py = y + 0.5f;
                    float halfWidth = py < 92f ? 40f + Mathf.Sin(py / 92f * Mathf.PI) * 6f : Mathf.Lerp(30f, 12f, (py - 92f) / 14f);
                    bool body = py > 12f && py < 106f && Mathf.Abs(px) < halfWidth;
                    bool tie = py >= 106f && py < 118f && Mathf.Abs(px) < 16f - (py - 106f) * 0.4f;
                    if (!body && !tie) continue;
                    var c = Scale(sack, 0.9f + 0.2f * (1f - Mathf.Abs(px) / 46f));
                    if (body && py > 44f && py < 66f) c = band;
                    if (tie && py < 110f) c = new Color32(150, 110, 70, 255);
                    pixels[y * size + x] = c;
                }
            }
            else if (style == ProductStyle.Bread)
            {
                // Oval loaf: darker crust, lighter top, three diagonal scores.
                var crust = new Color32(190, 120, 55, 255);
                var top = new Color32(225, 165, 85, 255);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - 64f) / 54f, dy = (y + 0.5f - 58f) / 34f;
                    float d = dx * dx + dy * dy;
                    if (d > 1f) continue;
                    var c = dy > -0.1f ? Scale(top, 1f - d * 0.15f) : crust;
                    float score = (x - 1.2f * y) % 26f;
                    if (dy > 0f && d < 0.75f && score > -3f && score < 3f && Mathf.Abs(dx) < 0.7f) c = Scale(crust, 0.85f);
                    pixels[y * size + x] = c;
                }
            }
            else
            {
                // Square golden corn bread with small kernels.
                var body = new Color32(235, 190, 70, 255);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = Mathf.Abs(x + 0.5f - 64f), py = Mathf.Abs(y + 0.5f - 60f);
                    bool inside = px < 46f && py < 38f && (px < 38f || py < 30f || (px - 38f) * (px - 38f) + (py - 30f) * (py - 30f) < 64f);
                    if (!inside) continue;
                    var c = y > 60 ? Scale(body, 1.08f) : Scale(body, 0.88f);
                    if (y > 64 && ((x / 9) + (y / 9)) % 3 == 0 && x % 9 < 4 && y % 9 < 4) c = new Color32(250, 225, 120, 255);
                    pixels[y * size + x] = c;
                }
            }

            AddOutline(pixels, size, size);
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        // ------------------------------------------------------------------ Orchard (Milestone 7 step 3)

        public enum FruitStyle { Apple, Cherry }

        /// <summary>The decoration tree (AI art once imported). Fruit trees are drawn from it so they match its style.</summary>
        public const string TreeSourcePath = DecorationsArtFolder + "/PH_Building_tree.png";

        /// <summary>Fruit tree building sprite: the tree with ripe fruit (shop icon, placement preview).</summary>
        public static Sprite CreateFruitTree(string treeId, FruitStyle style) =>
            CreateTreeVariant($"{FarmingBuildingsFolder}/PH_Building_{treeId}.png", style, ripe: true);

        /// <summary>Fruit growth sprites: [0] the tree with small unripe fruit, [1] ripe. Same canvas and pivot as the tree.</summary>
        public static Sprite[] CreateFruitStages(string cropId, FruitStyle style) => new[]
        {
            CreateTreeVariant($"{CropsArtFolder}/PH_Crop_{cropId}_1.png", style, ripe: false),
            CreateTreeVariant($"{CropsArtFolder}/PH_Crop_{cropId}_2.png", style, ripe: true),
        };

        private static Sprite CreateTreeVariant(string path, FruitStyle style, bool ripe)
        {
            var source = AssetDatabase.LoadAssetAtPath<Sprite>(TreeSourcePath);
            var pivot = source != null
                ? new Vector2(source.pivot.x / source.rect.width, source.pivot.y / source.rect.height)
                : new Vector2(0.5f, 0f);
            return CreateSprite(path, () => RenderTreeVariant(style, ripe), pivot, SpriteMeshType.Tight);
        }

        private static Texture2D RenderTreeVariant(FruitStyle style, bool ripe)
        {
            var src = new Texture2D(2, 2);
            src.LoadImage(File.ReadAllBytes(Path.Combine(Directory.GetCurrentDirectory(), TreeSourcePath)));
            int w = src.width, h = src.height;
            var px = src.GetPixels32();
            Object.DestroyImmediate(src);

            // Leaves: green, opaque, in the upper 70 % (not the trunk or grass at the base).
            var mask = new bool[w * h];
            for (int y = (int)(h * 0.3f); y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var c = px[y * w + x];
                mask[y * w + x] = c.a > 200 && c.g > c.r + 12 && c.g > c.b + 8;
            }

            if (style == FruitStyle.Cherry)
                for (int i = 0; i < px.Length; i++)
                    if (mask[i]) px[i] = new Color32((byte)(px[i].r * 0.78f), (byte)(px[i].g * 0.86f), (byte)Mathf.Min(255, px[i].b * 0.95f), px[i].a);

            // Fruit spots: on the leaves, spread out, the same spots for both stages (deterministic).
            bool apple = style == FruitStyle.Apple;
            float r = w * (apple ? 0.062f : 0.032f);
            int want = apple ? 8 : 7;

            bool OnLeaves(float cx, float cy, float rad)
            {
                for (int a = 0; a < 8; a++)
                {
                    float ang = a * Mathf.PI / 4f;
                    int x = Mathf.RoundToInt(cx + Mathf.Cos(ang) * rad), y = Mathf.RoundToInt(cy + Mathf.Sin(ang) * rad);
                    if (x < 0 || y < 0 || x >= w || y >= h || !mask[y * w + x]) return false;
                }

                int ix = (int)cx, iy = (int)cy;
                return ix >= 0 && iy >= 0 && ix < w && iy < h && mask[iy * w + ix];
            }

            var candidates = new List<(float x, float y, float score)>();
            int step = Mathf.Max(2, Mathf.RoundToInt(r * 1.2f));
            for (int y = step; y < h - step; y += step)
            for (int x = step; x < w - step; x += step)
            {
                float jx = x + (Hash(x, y) - 0.5f) * step, jy = y + (Hash(y, x) - 0.5f) * step;
                if (OnLeaves(jx, jy, r * (apple ? 1.6f : 2.2f))) candidates.Add((jx, jy, Hash(x * 7, y * 13)));
            }

            candidates.Sort((a, b) => a.score.CompareTo(b.score));
            var spots = new List<Vector2>();
            foreach (var c in candidates)
            {
                var p = new Vector2(c.x, c.y);
                bool far = true;
                foreach (var q in spots)
                    if ((q - p).sqrMagnitude < r * r * 16f) { far = false; break; }
                if (!far) continue;
                spots.Add(p);
                if (spots.Count >= want) break;
            }

            var ripeColour = apple ? new Color32(214, 40, 36, 255) : new Color32(150, 14, 42, 255);
            var unripe = apple ? new Color32(160, 196, 72, 255) : new Color32(176, 204, 92, 255);
            var stem = new Color32(70, 90, 40, 255);

            void Ball(Vector2 c, float rad, Color32 col)
            {
                for (int y = Mathf.Max(0, (int)(c.y - rad - 1)); y <= Mathf.Min(h - 1, (int)(c.y + rad + 1)); y++)
                for (int x = Mathf.Max(0, (int)(c.x - rad - 1)); x <= Mathf.Min(w - 1, (int)(c.x + rad + 1)); x++)
                {
                    float dx = (x + 0.5f - c.x) / rad, dy = (y + 0.5f - c.y) / rad;
                    float d = dx * dx + dy * dy;
                    if (d > 1f) continue;
                    float light = 0.82f + 0.3f * Mathf.Clamp01(0.5f + 0.5f * (dy - dx) * 0.7f);
                    var shade = d > 0.72f ? Scale(col, 0.62f) : Scale(col, light);
                    float hx = dx + 0.35f, hy = dy - 0.38f;
                    if (hx * hx + hy * hy < 0.06f) shade = new Color32(255, 240, 230, 255);
                    px[y * w + x] = shade;
                }
            }

            void Line(Vector2 a, Vector2 b, float thick, Color32 col)
            {
                int n = Mathf.CeilToInt((b - a).magnitude * 2f) + 1;
                for (int i = 0; i <= n; i++)
                {
                    var p = Vector2.Lerp(a, b, i / (float)n);
                    for (int y = (int)(p.y - thick); y <= (int)(p.y + thick); y++)
                    for (int x = (int)(p.x - thick); x <= (int)(p.x + thick); x++)
                        if (x >= 0 && y >= 0 && x < w && y < h && (x - p.x) * (x - p.x) + (y - p.y) * (y - p.y) <= thick * thick)
                            px[y * w + x] = col;
                }
            }

            foreach (var spot in spots)
            {
                var col = ripe ? ripeColour : unripe;
                float rad = ripe ? r : r * 0.7f;
                if (apple)
                {
                    Ball(spot, rad, col);
                    continue;
                }

                // Cherries hang in pairs from one stem.
                var top = spot + new Vector2(0f, rad * 2.2f);
                var left = spot + new Vector2(-rad * 0.95f, 0f);
                var right = spot + new Vector2(rad * 0.95f, -rad * 0.3f);
                Line(left, top, Mathf.Max(0.6f, rad * 0.18f), stem);
                Line(right, top, Mathf.Max(0.6f, rad * 0.18f), stem);
                Ball(left, rad, col);
                Ball(right, rad, col);
            }

            var tex = NewTexture(w, h);
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        /// <summary>Apple, cherries and juice bottles (128x128 icons).</summary>
        private static void RenderOrchardIcon(Color32[] pixels, int size, ProductStyle style)
        {
            void Ball(float cx, float cy, float rad, Color32 col)
            {
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - cx) / rad, dy = (y + 0.5f - cy) / rad;
                    if (dx * dx + dy * dy > 1f) continue;
                    var c = Scale(col, 0.8f + 0.35f * Mathf.Clamp01(0.5f + 0.5f * (dy - dx) * 0.7f));
                    float hx = dx + 0.38f, hy = dy - 0.4f;
                    if (hx * hx + hy * hy < 0.04f) c = new Color32(255, 245, 240, 255);
                    pixels[y * size + x] = c;
                }
            }

            void Line(Vector2 a, Vector2 b, float thick, Color32 col)
            {
                int n = Mathf.CeilToInt((b - a).magnitude * 2f) + 1;
                for (int i = 0; i <= n; i++)
                {
                    var p = Vector2.Lerp(a, b, i / (float)n);
                    for (int y = (int)(p.y - thick); y <= (int)(p.y + thick); y++)
                    for (int x = (int)(p.x - thick); x <= (int)(p.x + thick); x++)
                        if (x >= 0 && y >= 0 && x < size && y < size && (x - p.x) * (x - p.x) + (y - p.y) * (y - p.y) <= thick * thick)
                            pixels[y * size + x] = col;
                }
            }

            void Leaf(float cx, float cy, float rx, float ry)
            {
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1f) pixels[y * size + x] = dy > 0 ? new Color32(110, 180, 60, 255) : new Color32(80, 150, 46, 255);
                }
            }

            if (style == ProductStyle.Apple)
            {
                Ball(64, 56, 42, new Color32(214, 40, 36, 255));
                Line(new Vector2(64, 92), new Vector2(68, 112), 3f, new Color32(110, 76, 40, 255));
                Leaf(84, 106, 15, 7);
                return;
            }

            if (style == ProductStyle.Cherry)
            {
                var stem = new Color32(70, 110, 40, 255);
                Line(new Vector2(42, 56), new Vector2(72, 112), 2.5f, stem);
                Line(new Vector2(88, 50), new Vector2(72, 112), 2.5f, stem);
                Ball(42, 38, 25, new Color32(160, 16, 44, 255));
                Ball(88, 32, 25, new Color32(160, 16, 44, 255));
                Leaf(88, 108, 16, 7);
                return;
            }

            // Juice bottle: glass, juice up to the shoulder, a label with the fruit, a cap.
            bool appleJuice = style == ProductStyle.AppleJuice;
            var juice = appleJuice ? new Color32(242, 178, 40, 255) : new Color32(170, 22, 52, 255);
            var glass = new Color32(214, 236, 244, 255);
            var cap = appleJuice ? new Color32(80, 160, 60, 255) : new Color32(230, 190, 60, 255);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;
                if (fy < 8f || fy > 122f) continue;
                float half = fy <= 84f ? 30f : fy <= 100f ? Mathf.Lerp(30f, 11f, (fy - 84f) / 16f) : 11f;
                if (fy < 18f && Mathf.Abs(fx - 64f) > 20f)
                {
                    float cx = fx < 64f ? 44f : 84f, ddx = fx - cx, ddy = fy - 18f;
                    if (ddx * ddx + ddy * ddy > 100f) continue;
                }

                if (Mathf.Abs(fx - 64f) > half) continue;
                Color32 c = fy > 112f ? cap
                    : fy <= 88f ? Scale(juice, 0.85f + 0.3f * Mathf.Clamp01((fx - 34f) / 60f))
                    : glass;
                if (fx > 40f && fx < 46f && fy > 20f && fy < 80f) c = Scale(c, 1.25f); // shine
                pixels[y * size + x] = c;
            }

            for (int y = 34; y < 62; y++)
            for (int x = 34; x < 94; x++)
                pixels[y * size + x] = new Color32(250, 246, 232, 255);
            if (appleJuice)
            {
                Ball(64, 48, 10, new Color32(214, 40, 36, 255));
            }
            else
            {
                Ball(58, 46, 7, new Color32(160, 16, 44, 255));
                Ball(71, 44, 7, new Color32(160, 16, 44, 255));
            }
        }

        // ------------------------------------------------------------------ Build window (v0.27.4)

        public const string AwningPath = UiArtFolder + "/Build/PH_UI_Awning_Green.png";
        public const string WoodPath = UiArtFolder + "/Build/PH_UI_Wood.png";
        public const string CirclePath = UiArtFolder + "/Build/PH_UI_Circle.png";
        public const string MenuIconPath = UiArtFolder + "/PH_Icon_Menu.png";

        /// <summary>Build window pieces drawn in code: an awning stripe pair (tiled along the top) and a round badge.</summary>
        public static void CreateBuildScreenArt()
        {
            CreateSprite(AwningPath, RenderAwning, new Vector2(0.5f, 0.5f), SpriteMeshType.FullRect);
            CreateSprite(WoodPath, RenderWood, new Vector2(0.5f, 0.5f), SpriteMeshType.FullRect);
            CreateSprite(CirclePath, RenderCircle, new Vector2(0.5f, 0.5f), SpriteMeshType.FullRect);
            CreateSprite(MenuIconPath, RenderMenuIcon, new Vector2(0.5f, 0.5f), SpriteMeshType.FullRect);
        }

        /// <summary>Three white rounded bars (menu icon), with a soft dark edge.</summary>
        private static Texture2D RenderMenuIcon()
        {
            const int w = 72, h = 60;
            var tex = NewTexture(w, h);
            var px = new Color32[w * h];
            foreach (float cy in new[] { 10f, 30f, 50f })
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = Mathf.Max(0f, Mathf.Abs(x + 0.5f - w / 2f) - 26f), dy = Mathf.Abs(y + 0.5f - cy);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > 8f) continue;
                px[y * w + x] = d > 6.5f ? new Color32(20, 60, 120, 255) : new Color32(255, 255, 255, 255);
            }

            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        /// <summary>One blue and one white stripe (96x72), scalloped at the bottom; tiles seamlessly sideways.</summary>
        private static Texture2D RenderAwning()
        {
            const int w = 96, h = 72, stripe = 48;
            var tex = NewTexture(w, h);
            var px = new Color32[w * h];
            var blue = new Color(0.30f, 0.70f, 0.26f); // stripe colour (green)
            var white = new Color(0.99f, 0.99f, 0.96f);
            var rim = new Color(0.14f, 0.40f, 0.12f);
            for (int x = 0; x < w; x++)
            {
                bool isBlue = x < stripe;
                float u = (x % stripe + 0.5f - stripe / 2f) / (stripe / 2f);
                float edge = 18f - 16f * Mathf.Sqrt(Mathf.Max(0f, 1f - u * u)); // scallop: lowest in the middle of a stripe
                for (int y = 0; y < h; y++)
                {
                    if (y < edge) continue;
                    var c = isBlue ? blue : white;
                    float shade = Mathf.Lerp(0.82f, 1.04f, (y - edge) / (h - edge)); // darker towards the bottom
                    c = new Color(c.r * shade, c.g * shade, c.b * shade);
                    if (y < edge + 3f) c = Color.Lerp(c, rim, 0.75f);           // dark edge along the scallops
                    if (y > h - 6) c = Color.Lerp(c, rim, 0.35f);               // band along the top
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            }

            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        /// <summary>Warm wooden planks (128x128, tiles seamlessly): horizontal grain, a plank seam, a few knots.</summary>
        private static Texture2D RenderWood()
        {
            const int size = 128;
            var tex = NewTexture(size, size);
            var px = new Color32[size * size];
            var light = new Color(0.80f, 0.53f, 0.27f);
            var dark = new Color(0.60f, 0.36f, 0.16f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                // Grain: wavy stripes that repeat exactly across the tile.
                float g = Mathf.Sin((v * 9f + 0.35f * Mathf.Sin(u * Mathf.PI * 2f) + 0.12f * Mathf.Sin(u * Mathf.PI * 6f)) * Mathf.PI * 2f);
                var c = Color.Lerp(dark, light, 0.55f + 0.3f * g);
                c *= 0.95f + 0.1f * Hash(x, y / 3);
                if (y % 64 < 3) c = dark * 0.75f;                    // seam between planks
                else if (y % 64 < 6) c = Color.Lerp(c, light, 0.5f); // lit edge under the seam
                float kx = (x - 40) / 7f, ky = (y - 92) / 4f;       // a knot
                if (kx * kx + ky * ky < 1f) c = dark * 0.85f;
                c.a = 1f;
                px[y * size + x] = c;
            }

            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        /// <summary>White disc with a soft darker rim and a white ring (tinted red for badges).</summary>
        private static Texture2D RenderCircle()
        {
            const int size = 96;
            var tex = NewTexture(size, size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - size / 2f, dy = y + 0.5f - size / 2f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / (size / 2f);
                if (d > 1f) continue;
                float a = Mathf.Clamp01((1f - d) * size / 2f); // anti-aliased edge
                Color c = d > 0.86f ? new Color(1f, 1f, 1f) : d > 0.78f ? new Color(0.62f, 0.62f, 0.62f) : new Color(0.92f, 0.92f, 0.92f);
                if (d <= 0.78f && dy > 0) c = Color.Lerp(c, Color.white, dy / size); // gloss on top
                c.a = a;
                px[y * size + x] = c;
            }

            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        // ------------------------------------------------------------------ Helpers

        private static Sprite CreateSprite(string assetPath, System.Func<Texture2D> render, Vector2 pivot, SpriteMeshType meshType)
        {
            EnsureFolder(Path.GetDirectoryName(assetPath).Replace('\\', '/'));
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);

            if (!File.Exists(fullPath))
            {
                var tex = render();
                File.WriteAllBytes(fullPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                ConfigureSpriteImporter(assetPath, pivot, meshType);
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        /// <summary>Standard import settings for every world sprite (see Documentation/AssetImportGuide.md).</summary>
        public static void ConfigureSpriteImporter(string assetPath, Vector2 pivot, SpriteMeshType meshType)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ArtConventions.PixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Compressed;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            settings.spriteMeshType = meshType;
            importer.SetTextureSettings(settings);

            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.format = TextureImporterFormat.ASTC_6x6;
            android.maxTextureSize = 2048;
            importer.SetPlatformTextureSettings(android);

            importer.SaveAndReimport();
        }

        public static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        private static Texture2D NewTexture(int w, int h) =>
            new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };

        private static bool Inside(Vector2[] poly, Vector2 p) => IsoMath.ConvexContains(poly, p);

        private static Color32 Scale(Color32 c, float f) =>
            new Color32((byte)Mathf.Clamp(c.r * f, 0, 255), (byte)Mathf.Clamp(c.g * f, 0, 255), (byte)Mathf.Clamp(c.b * f, 0, 255), c.a);

        private static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }
    }
}
