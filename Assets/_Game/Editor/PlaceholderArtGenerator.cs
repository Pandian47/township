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
