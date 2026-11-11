using System.Collections.Generic;
using System.IO;
using TownBuilder.Domain;
using UnityEditor;
using UnityEngine;
using Terrain = TownBuilder.Domain.Terrain;

namespace TownBuilder.EditorTools
{
    /// <summary>
    /// Joined roads (v0.36.0). For every road style, draws 16 pictures from the style's flat tile
    /// (Art/Environment/Tiles/PH_Tile_&lt;Style&gt;.png, code-drawn or the owner's AI texture): one per combination of
    /// neighbouring roads, with a kerb along every side that has no road. Writes them to Tiles/Roads and fills a
    /// <see cref="JoinedRoadTile"/> asset per style. Run by ProjectSetup (missing pictures only) and by the art import
    /// (styles whose texture was re-imported).
    /// </summary>
    public static class RoadTileBuilder
    {
        public const string Folder = PlaceholderArtGenerator.TilesArtFolder + "/Roads";

        /// <summary>Kerb colour and the thin line between kerb and road, per style.</summary>
        private static readonly Dictionary<Terrain, (Color32 kerb, Color32 line)> Kerbs = new Dictionary<Terrain, (Color32, Color32)>
        {
            { Terrain.Road, (new Color32(226, 220, 206, 255), new Color32(118, 110, 98, 255)) },
            { Terrain.DirtPath, (new Color32(104, 162, 70, 255), new Color32(122, 92, 60, 255)) },   // grass verge
            { Terrain.BrickLane, (new Color32(208, 202, 192, 255), new Color32(110, 100, 94, 255)) },
            { Terrain.Boardwalk, (new Color32(116, 74, 40, 255), new Color32(72, 44, 22, 255)) },   // edge beam
        };

        private const float Band = 0.15f;      // kerb width, as a share of the distance from the tile centre to its edge
        private const float LineBand = 0.035f;

        public static string TilePath(Terrain t) => $"{PlaceholderArtGenerator.TileAssetsFolder}/PH_RoadTile_{t}.asset";

        /// <summary>Builds every road style; styles in <paramref name="redraw"/> are redrawn even if their pictures exist.</summary>
        public static void BuildAll(IEnumerable<Terrain> redraw = null)
        {
            PlaceholderArtGenerator.EnsureFolder(Folder);
            var force = new HashSet<Terrain>(redraw ?? new Terrain[0]);
            foreach (var style in Kerbs.Keys) Build(style, force.Contains(style));
            AssetDatabase.SaveAssets();
        }

        private static void Build(Terrain style, bool force)
        {
            var sourcePath = $"{PlaceholderArtGenerator.TilesArtFolder}/{PlaceholderArtGenerator.TileNameFor(style)}.png";
            if (!File.Exists(sourcePath))
            {
                Debug.LogWarning($"[Roads] Missing {sourcePath}; run the full setup first.");
                return;
            }

            Texture2D source = null;
            var sprites = new Sprite[16];
            for (int mask = 0; mask < 16; mask++)
            {
                var path = $"{Folder}/PH_Road_{style}_{mask:00}.png";
                if (force || !File.Exists(path))
                {
                    if (source == null)
                    {
                        source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        source.LoadImage(File.ReadAllBytes(sourcePath));
                    }

                    var tex = Render(source, mask, Kerbs[style]);
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    PlaceholderArtGenerator.ConfigureSpriteImporter(path, new Vector2(0.5f, 0.5f), SpriteMeshType.FullRect);
                }

                sprites[mask] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }

            if (source != null) Object.DestroyImmediate(source);

            var tilePath = TilePath(style);
            var tile = AssetDatabase.LoadAssetAtPath<JoinedRoadTile>(tilePath);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<JoinedRoadTile>();
                AssetDatabase.CreateAsset(tile, tilePath);
            }

            tile.EditorSetSprites(sprites);
            EditorUtility.SetDirty(tile);
        }

        /// <summary>The flat tile with a kerb along each side whose bit is not set in <paramref name="mask"/>.</summary>
        private static Texture2D Render(Texture2D source, int mask, (Color32 kerb, Color32 line) colours)
        {
            int w = source.width, h = source.height;
            var src = source.GetPixels32();
            var px = new Color32[src.Length];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                var c = src[i];
                px[i] = c;
                if (c.a == 0) continue;
                float dx = (x + 0.5f - w * 0.5f) / (w * 0.5f), dy = (y + 0.5f - h * 0.5f) / (h * 0.5f);
                // Distance towards each side (1 = on that edge): +X up-right, +Y up-left, -X down-left, -Y down-right.
                float best = float.MinValue;
                if ((mask & RoadJoins.PlusX) == 0) best = Mathf.Max(best, dx + dy);
                if ((mask & RoadJoins.PlusY) == 0) best = Mathf.Max(best, -dx + dy);
                if ((mask & RoadJoins.MinusX) == 0) best = Mathf.Max(best, -dx - dy);
                if ((mask & RoadJoins.MinusY) == 0) best = Mathf.Max(best, dx - dy);
                if (best > 1f - Band)
                {
                    float shade = 0.94f + 0.12f * Mathf.Repeat((x * 7 + y * 13) * 0.137f, 1f);
                    var k = colours.kerb;
                    px[i] = new Color32((byte)Mathf.Min(255, k.r * shade), (byte)Mathf.Min(255, k.g * shade), (byte)Mathf.Min(255, k.b * shade), c.a);
                }
                else if (best > 1f - Band - LineBand)
                {
                    px[i] = new Color32(colours.line.r, colours.line.g, colours.line.b, c.a);
                }
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
