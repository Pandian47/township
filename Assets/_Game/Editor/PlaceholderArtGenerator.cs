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

        public enum CropStyle { Wheat, Corn, Soybean }

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
                : style == CropStyle.Soybean ? new[] { 5f, 10f, 15f, 18f } : new[] { 9f, 22f, 36f, 46f };
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

        public enum ProductStyle { Bread, CornBread, ChickenFeed, CowFeed, Egg, Milk }

        /// <summary>128x128 product icon (UI and the "ready" bubble above factories). Pivot centre.</summary>
        public static Sprite CreateProductIcon(string productId, ProductStyle style) =>
            CreateSprite($"{ProductsArtFolder}/PH_Product_{productId}.png", () => RenderProduct(style), new Vector2(0.5f, 0.5f), SpriteMeshType.FullRect);

        private static Texture2D RenderProduct(ProductStyle style)
        {
            const int size = 128;
            var tex = NewTexture(size, size);
            var pixels = new Color32[size * size];

            if (style == ProductStyle.Egg)
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
