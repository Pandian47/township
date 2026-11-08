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

        /// <summary>
        /// The same building with a red train (three cars) along its front-left side: the Train Station sprite while
        /// the train waits at the platform. Same size and pivot as the plain building.
        /// </summary>
        public static Sprite CreateBuildingWithTrain(BuildingArtSpec spec, string path)
        {
            var pivot = new Vector2((float)spec.FootprintY / (spec.FootprintX + spec.FootprintY), 0f);
            return CreateSprite(path, () =>
            {
                var tex = RenderBuilding(spec);
                PaintTrain(tex, spec);
                return tex;
            }, pivot, SpriteMeshType.Tight);
        }

        private static void PaintTrain(Texture2D tex, BuildingArtSpec s)
        {
            var pixels = tex.GetPixels32();
            int w = tex.width, h = tex.height;
            var B = new Vector2(s.FootprintY * HalfW, 0);
            var L = B + new Vector2(-s.FootprintY * HalfW, s.FootprintY * HalfH);
            const float trainHeight = 46f;
            var body = new Color32(196, 54, 44, 255);
            var loco = new Color32(60, 66, 80, 255);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float t = (x + 0.5f - L.x) / (B.x - L.x);
                if (t < 0.03f || t > 0.97f) continue;
                float carT = t * 3f % 1f;
                if (carT < 0.04f) continue; // gap between cars
                float baseY = L.y + (B.y - L.y) * t;
                float dy = y + 0.5f - baseY - 3f;
                if (dy < 0f || dy > trainHeight) continue;
                bool isLoco = t > 0.66f;
                Color32 c = dy < 7f ? new Color32(40, 40, 46, 255)
                    : dy > 22f && dy < 34f && carT > 0.15f && carT < 0.85f && (int)(carT * 10f) % 3 != 0 ? new Color32(250, 236, 170, 255)
                    : isLoco ? loco : body;
                if (dy > trainHeight - 4f) c = Scale(c, 0.8f);
                pixels[y * w + x] = c;
            }

            tex.SetPixels32(pixels);
            tex.Apply();
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

        public enum ProductStyle { Bread, CornBread, ChickenFeed, CowFeed, Egg, Milk, Sugar, Cookies, Cake, Butter, Cheese, Planks, Bricks, Nails }

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
