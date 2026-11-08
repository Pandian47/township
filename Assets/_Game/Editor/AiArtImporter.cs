using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TownBuilder.EditorTools
{
    /// <summary>
    /// Turns raw AI-generated images (one object on a flat magenta background, any size) into game-ready sprites:
    /// removes the background, trims, scales to the exact footprint width, puts the footprint's bottom corner at the
    /// sprite pivot, and overwrites the matching placeholder PNG (so every reference and import setting is kept).
    /// Square top-down ground textures are converted into 128x64 isometric diamond tiles.
    ///
    /// Menu: TownBuilder > Art > Import AI Art Folder...
    /// Command line: -executeMethod TownBuilder.EditorTools.AiArtImporter.ImportFromCommandLine -artSource "D:\path"
    /// Expected file names: see <see cref="Specs"/> and Documentation/ArtPrompts.md.
    /// </summary>
    public static class AiArtImporter
    {
        private enum Kind
        {
            /// <summary>Building: footprint width measured on the lower walls (roof eaves may overhang).</summary>
            Building,
            /// <summary>Flat footprint object (field): width measured on the whole object.</summary>
            Flat,
            /// <summary>Plants drawn on top of a field: fitted inside the field's soil, same canvas as the field.</summary>
            Crop,
            /// <summary>Item icon: fitted into a square.</summary>
            Icon,
            /// <summary>Square top-down texture converted into an isometric diamond tile.</summary>
            Tile,
        }

        private sealed class Spec
        {
            public string Source;
            public string Target;
            public Kind Kind;
            public int Width; // footprint width (Building/Flat), canvas width (Crop), square size (Icon)
            public int FootprintX = 1, FootprintY = 1;
            /// <summary>Set by processing: sprite pivot (bottom corner of the footprint for buildings).</summary>
            public Vector2 Pivot = new Vector2(0.5f, 0f);

            public Spec(string source, string target, Kind kind, int width, int footprintX = 1, int footprintY = 1)
            {
                Source = source;
                Target = target;
                Kind = kind;
                Width = width;
                FootprintX = footprintX;
                FootprintY = footprintY;
            }
        }

        private const string Art = "Assets/_Game/Art";

        // Footprint widths follow AssetImportGuide.md: (w + h) * 64 px.
        private static readonly Spec[] Specs =
        {
            new Spec("cottage", Art + "/Buildings/Residential/PH_Building_cottage.png", Kind.Building, 256),
            new Spec("town_hall", Art + "/Buildings/Community/PH_Building_town_hall.png", Kind.Building, 384),
            new Spec("barn", Art + "/Buildings/Production/PH_Building_barn.png", Kind.Building, 256),
            new Spec("bakery", Art + "/Buildings/Production/PH_Building_bakery.png", Kind.Building, 256),
            new Spec("train_station", Art + "/Buildings/Community/PH_Building_train_station.png", Kind.Building, 320, 3, 2),
            new Spec("train_station_train", Art + "/Buildings/Community/PH_Building_train_station_train.png", Kind.Building, 320, 3, 2),
            new Spec("silo", Art + "/Buildings/Production/PH_Building_silo.png", Kind.Building, 128),
            new Spec("feed_mill", Art + "/Buildings/Production/PH_Building_feed_mill.png", Kind.Building, 256),
            new Spec("sugar_mill", Art + "/Buildings/Production/PH_Building_sugar_mill.png", Kind.Building, 256),
            new Spec("creamery", Art + "/Buildings/Production/PH_Building_creamery.png", Kind.Building, 256),
            new Spec("chicken_coop", Art + "/Buildings/Farming/PH_Building_chicken_coop.png", Kind.Building, 256),
            new Spec("dairy_shed", Art + "/Buildings/Farming/PH_Building_dairy_shed.png", Kind.Building, 256),
            new Spec("field", Art + "/Buildings/Farming/PH_Building_field.png", Kind.Flat, 128),
            new Spec("wheat_1", Art + "/Crops/PH_Crop_wheat_1.png", Kind.Crop, 128),
            new Spec("wheat_2", Art + "/Crops/PH_Crop_wheat_2.png", Kind.Crop, 128),
            new Spec("wheat_3", Art + "/Crops/PH_Crop_wheat_3.png", Kind.Crop, 128),
            new Spec("wheat_4", Art + "/Crops/PH_Crop_wheat_4.png", Kind.Crop, 128),
            new Spec("corn_1", Art + "/Crops/PH_Crop_corn_1.png", Kind.Crop, 128),
            new Spec("corn_2", Art + "/Crops/PH_Crop_corn_2.png", Kind.Crop, 128),
            new Spec("corn_3", Art + "/Crops/PH_Crop_corn_3.png", Kind.Crop, 128),
            new Spec("corn_4", Art + "/Crops/PH_Crop_corn_4.png", Kind.Crop, 128),
            new Spec("soybean_1", Art + "/Crops/PH_Crop_soybean_1.png", Kind.Crop, 128),
            new Spec("soybean_2", Art + "/Crops/PH_Crop_soybean_2.png", Kind.Crop, 128),
            new Spec("soybean_3", Art + "/Crops/PH_Crop_soybean_3.png", Kind.Crop, 128),
            new Spec("soybean_4", Art + "/Crops/PH_Crop_soybean_4.png", Kind.Crop, 128),
            new Spec("sugarcane_1", Art + "/Crops/PH_Crop_sugarcane_1.png", Kind.Crop, 128),
            new Spec("sugarcane_2", Art + "/Crops/PH_Crop_sugarcane_2.png", Kind.Crop, 128),
            new Spec("sugarcane_3", Art + "/Crops/PH_Crop_sugarcane_3.png", Kind.Crop, 128),
            new Spec("sugarcane_4", Art + "/Crops/PH_Crop_sugarcane_4.png", Kind.Crop, 128),
            new Spec("bread", Art + "/Products/PH_Product_bread.png", Kind.Icon, 128),
            new Spec("corn_bread", Art + "/Products/PH_Product_corn_bread.png", Kind.Icon, 128),
            new Spec("chicken_feed", Art + "/Products/PH_Product_chicken_feed.png", Kind.Icon, 128),
            new Spec("cow_feed", Art + "/Products/PH_Product_cow_feed.png", Kind.Icon, 128),
            new Spec("egg", Art + "/Products/PH_Product_egg.png", Kind.Icon, 128),
            new Spec("milk", Art + "/Products/PH_Product_milk.png", Kind.Icon, 128),
            new Spec("sugar", Art + "/Products/PH_Product_sugar.png", Kind.Icon, 128),
            new Spec("cookies", Art + "/Products/PH_Product_cookies.png", Kind.Icon, 128),
            new Spec("cake", Art + "/Products/PH_Product_cake.png", Kind.Icon, 128),
            new Spec("butter", Art + "/Products/PH_Product_butter.png", Kind.Icon, 128),
            new Spec("cheese", Art + "/Products/PH_Product_cheese.png", Kind.Icon, 128),
            new Spec("planks", Art + "/Materials/PH_Material_planks.png", Kind.Icon, 128),
            new Spec("bricks", Art + "/Materials/PH_Material_bricks.png", Kind.Icon, 128),
            new Spec("nails", Art + "/Materials/PH_Material_nails.png", Kind.Icon, 128),
            new Spec("grass", Art + "/Environment/Tiles/PH_Tile_Grass.png", Kind.Tile, 128),
            new Spec("sand", Art + "/Environment/Tiles/PH_Tile_Sand.png", Kind.Tile, 128),
            new Spec("water", Art + "/Environment/Tiles/PH_Tile_Water.png", Kind.Tile, 128),
            new Spec("road", Art + "/Environment/Tiles/PH_Tile_Road.png", Kind.Tile, 128),
        };

        // Crop plants sit inside the field's soil: narrower than the field and lifted by the field's border height.
        private const float CropWidthFraction = 0.74f;
        private const int CropBottomOffset = 14;

        // Background keying (0..1 colour distance from the magenta background).
        private const float KeyInner = 0.14f;
        private const float KeyOuter = 0.32f;

        [MenuItem("TownBuilder/Art/Import AI Art Folder...", priority = 30)]
        public static void ImportMenu()
        {
            string folder = EditorUtility.OpenFolderPanel("Folder with AI images (cottage.png, field.png...)", "", "");
            if (string.IsNullOrEmpty(folder)) return;
            var report = Import(folder);
            EditorUtility.DisplayDialog("AI art import", report, "OK");
        }

        public static void ImportFromCommandLine()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                int i = Array.IndexOf(args, "-artSource");
                if (i < 0 || i + 1 >= args.Length) throw new ArgumentException("Pass -artSource <folder>");
                Debug.Log("[AiArt] " + Import(args[i + 1]));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Processes every known file found in <paramref name="folder"/>. Returns a short report.</summary>
        public static string Import(string folder)
        {
            if (!Directory.Exists(folder)) throw new DirectoryNotFoundException(folder);
            var done = new List<string>();
            var missing = new List<string>();

            foreach (var spec in Specs)
            {
                string source = FindSource(folder, spec.Source);
                if (source == null) { missing.Add(spec.Source); continue; }

                var image = Load(source);
                var output = Process(image, spec);
                WritePng(output, spec.Target);
                ConfigureImporter(spec);
                done.Add($"{spec.Source} -> {Path.GetFileName(spec.Target)} ({output.W}x{output.H})");
                Debug.Log($"[AiArt] {spec.Source}: {image.W}x{image.H} -> {spec.Target} ({output.W}x{output.H})");
            }

            AssetDatabase.SaveAssets();
            var unknown = Directory.GetFiles(folder, "*.png").Select(Path.GetFileNameWithoutExtension)
                .Where(n => Specs.All(s => !string.Equals(s.Source, n, StringComparison.OrdinalIgnoreCase))).ToList();

            return $"Imported {done.Count}: {string.Join(", ", done.Select(d => d.Split(' ')[0]))}." +
                   (missing.Count > 0 ? $" Not found (placeholders kept): {string.Join(", ", missing)}." : "") +
                   (unknown.Count > 0 ? $" Ignored unknown files: {string.Join(", ", unknown)}." : "");
        }

        // ------------------------------------------------------------------ Processing

        /// <summary>Straight (non-premultiplied) RGBA float image, row 0 = bottom (Unity texture order).</summary>
        private sealed class Img
        {
            public readonly int W, H;
            public readonly Color[] P;

            public Img(int w, int h)
            {
                W = w;
                H = h;
                P = new Color[w * h];
            }

            public Color this[int x, int y]
            {
                get => P[y * W + x];
                set => P[y * W + x] = value;
            }
        }

        private static Img Process(Img src, Spec spec)
        {
            if (spec.Kind == Kind.Tile) return MakeIsoTile(src, spec.Width);

            var keyed = KeyBackground(src);
            if (!FindBounds(keyed, out int minX, out int maxX, out int minY, out int maxY))
                throw new InvalidOperationException($"{spec.Source}: no object found on the background");

            switch (spec.Kind)
            {
                case Kind.Icon:
                {
                    int margin = Mathf.RoundToInt(spec.Width * 0.05f);
                    float size = Mathf.Max(maxX - minX + 1, maxY - minY + 1);
                    float scale = (spec.Width - 2 * margin) / size;
                    float cx = (minX + maxX + 1) * 0.5f, cy = (minY + maxY + 1) * 0.5f;
                    float half = spec.Width * 0.5f / scale;
                    return Resample(keyed, cx - half, cy - half, half * 2f, half * 2f, spec.Width, spec.Width);
                }

                case Kind.Crop:
                {
                    float plantWidth = spec.Width * CropWidthFraction;
                    float scale = plantWidth / (maxX - minX + 1);
                    float cx = (minX + maxX + 1) * 0.5f;
                    int outH = Mathf.CeilToInt((maxY - minY + 1) * scale) + CropBottomOffset;
                    float srcW = spec.Width / scale;
                    float srcBottom = minY - CropBottomOffset / scale;
                    return Resample(keyed, cx - srcW * 0.5f, srcBottom, srcW, outH / scale, spec.Width, outH);
                }

                default: // Building, Flat
                {
                    int left = minX, right = maxX;
                    if (spec.Kind == Kind.Building) MeasureBase(keyed, minY, maxY, out left, out right);
                    float scale = spec.Width / (float)(right - left + 1);
                    float cx = (left + right + 1) * 0.5f;
                    // Symmetric canvas around the footprint centre so the pivot (0.5, 0) stays the bottom corner.
                    float halfSrc = Mathf.Max(cx - minX, maxX + 1 - cx);
                    int outW = 2 * Mathf.CeilToInt(halfSrc * scale);
                    int outH = Mathf.CeilToInt((maxY - minY + 1) * scale);
                    float srcW = outW / scale;
                    // Bottom corner of a w×h footprint sits h/(w+h) of the way across the base (0.5 for square ones).
                    float cornerShare = spec.FootprintY / (float)(spec.FootprintX + spec.FootprintY);
                    spec.Pivot = new Vector2(0.5f + spec.Width * (cornerShare - 0.5f) / outW, 0f);
                    return Resample(keyed, cx - srcW * 0.5f, minY, srcW, outH / scale, outW, outH);
                }
            }
        }

        /// <summary>Background colour = median of the border pixels; alpha from the distance to it; colour unmixed at edges.</summary>
        private static Img KeyBackground(Img src)
        {
            var border = new List<Color>();
            for (int x = 0; x < src.W; x += 4) { border.Add(src[x, 0]); border.Add(src[x, src.H - 1]); }
            for (int y = 0; y < src.H; y += 4) { border.Add(src[0, y]); border.Add(src[src.W - 1, y]); }
            float Median(Func<Color, float> f) => border.Select(f).OrderBy(v => v).ElementAt(border.Count / 2);
            var bg = new Color(Median(c => c.r), Median(c => c.g), Median(c => c.b), 1f);

            var dst = new Img(src.W, src.H);
            for (int i = 0; i < src.P.Length; i++)
            {
                var c = src.P[i];
                float d = Mathf.Max(Mathf.Abs(c.r - bg.r), Mathf.Abs(c.g - bg.g), Mathf.Abs(c.b - bg.b));
                float a = Mathf.Clamp01((d - KeyInner) / (KeyOuter - KeyInner));
                if (a <= 0.02f) { dst.P[i] = new Color(0f, 0f, 0f, 0f); continue; }

                if (a < 1f)
                {
                    // Remove the background's share from blended edge pixels.
                    c.r = Mathf.Clamp01((c.r - (1f - a) * bg.r) / a);
                    c.g = Mathf.Clamp01((c.g - (1f - a) * bg.g) / a);
                    c.b = Mathf.Clamp01((c.b - (1f - a) * bg.b) / a);
                }

                dst.P[i] = new Color(c.r, c.g, c.b, a);
            }

            // Magenta fringe on pixels touching the background: pull red/blue down to green (keeps real pinks inside).
            var copy = (Color[])dst.P.Clone();
            for (int y = 1; y < dst.H - 1; y++)
            for (int x = 1; x < dst.W - 1; x++)
            {
                var c = copy[y * dst.W + x];
                if (c.a <= 0f) continue;
                bool edge = c.a < 1f;
                for (int oy = -2; oy <= 2 && !edge; oy++)
                for (int ox = -2; ox <= 2 && !edge; ox++)
                {
                    int nx = Mathf.Clamp(x + ox, 0, dst.W - 1), ny = Mathf.Clamp(y + oy, 0, dst.H - 1);
                    edge = copy[ny * dst.W + nx].a < 0.5f;
                }

                if (!edge) continue;
                float excess = Mathf.Min(c.r, c.b) - c.g;
                if (excess <= 0.04f) continue;
                c.r -= excess * 0.85f;
                c.b -= excess * 0.85f;
                dst[x, y] = c;
            }

            return dst;
        }

        /// <summary>Bounding box of the object, ignoring stray specks (rows/columns need a few opaque pixels).</summary>
        private static bool FindBounds(Img img, out int minX, out int maxX, out int minY, out int maxY)
        {
            const int minCount = 3;
            var rows = new int[img.H];
            var cols = new int[img.W];
            for (int y = 0; y < img.H; y++)
            for (int x = 0; x < img.W; x++)
            {
                if (img[x, y].a < 0.5f) continue;
                rows[y]++;
                cols[x]++;
            }

            minY = Array.FindIndex(rows, r => r >= minCount);
            maxY = Array.FindLastIndex(rows, r => r >= minCount);
            minX = Array.FindIndex(cols, c => c >= minCount);
            maxX = Array.FindLastIndex(cols, c => c >= minCount);
            return minX >= 0 && minY >= 0;
        }

        /// <summary>
        /// Footprint corners of a building: the widest point of its lower 40% (walls stand on the footprint; the roof
        /// higher up may overhang and must not count).
        /// </summary>
        private static void MeasureBase(Img img, int minY, int maxY, out int left, out int right)
        {
            int top = minY + Mathf.RoundToInt((maxY - minY) * 0.4f);
            left = int.MaxValue;
            right = int.MinValue;
            for (int y = minY; y <= top; y++)
            {
                int l = -1, r = -1, count = 0;
                for (int x = 0; x < img.W; x++)
                {
                    if (img[x, y].a < 0.5f) continue;
                    if (l < 0) l = x;
                    r = x;
                    count++;
                }

                if (count < 3) continue;
                left = Mathf.Min(left, l);
                right = Mathf.Max(right, r);
            }
        }

        /// <summary>Area-averaging resample of a source rectangle (premultiplied, so edges don't darken).</summary>
        private static Img Resample(Img src, float sx, float sy, float sw, float sh, int ow, int oh)
        {
            // Horizontal pass: src.H rows x ow columns.
            var tmp = new Color[ow * src.H];
            float stepX = sw / ow;
            for (int y = 0; y < src.H; y++)
            for (int ox = 0; ox < ow; ox++)
                tmp[y * ow + ox] = Integrate(sx + ox * stepX, stepX, x => Premul(src, x, y));

            var dst = new Img(ow, oh);
            float stepY = sh / oh;
            for (int oy = 0; oy < oh; oy++)
            for (int ox = 0; ox < ow; ox++)
            {
                var c = Integrate(sy + oy * stepY, stepY, y => y >= 0 && y < src.H ? tmp[y * ow + ox] : Color.clear);
                dst[ox, oy] = c.a > 0.0001f ? new Color(c.r / c.a, c.g / c.a, c.b / c.a, Mathf.Clamp01(c.a)) : Color.clear;
            }

            return dst;
        }

        private static Color Premul(Img img, int x, int y)
        {
            if (x < 0 || x >= img.W) return Color.clear;
            var c = img[x, y];
            return new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
        }

        /// <summary>Average of sample(i) over the continuous interval [start, start + length) with fractional end pixels.</summary>
        private static Color Integrate(float start, float length, Func<int, Color> sample)
        {
            if (length <= 1f)
            {
                // Upscaling or 1:1: bilinear between the two nearest pixels.
                float center = start + length * 0.5f - 0.5f;
                int i0 = Mathf.FloorToInt(center);
                float t = center - i0;
                return Color.Lerp(sample(i0), sample(i0 + 1), t);
            }

            float end = start + length;
            var sum = Color.clear;
            for (int i = Mathf.FloorToInt(start); i < Mathf.CeilToInt(end); i++)
            {
                float w = Mathf.Min(end, i + 1) - Mathf.Max(start, i);
                if (w > 0f) sum += sample(i) * w;
            }

            return sum / length;
        }

        /// <summary>Square, tileable top-down texture -> isometric diamond (width x width/2), slightly oversized to avoid seams.</summary>
        private static Img MakeIsoTile(Img src, int width)
        {
            int height = width / 2;
            const int prescale = 256;
            var small = Resample(src, 0, 0, src.W, src.H, prescale, prescale);
            var tile = new Img(width, height);
            const int ss = 4; // supersamples per axis
            float halfW = width * 0.5f, halfH = height * 0.5f;
            float grow = 1f + 1.5f / halfW; // ~1.5 px overlap with neighbouring tiles

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var sum = Color.clear;
                int inside = 0;
                for (int sy = 0; sy < ss; sy++)
                for (int sx = 0; sx < ss; sx++)
                {
                    float dx = (x + (sx + 0.5f) / ss) - halfW;
                    float dy = (y + (sy + 0.5f) / ss) - halfH;
                    if (Mathf.Abs(dx) / halfW + Mathf.Abs(dy) / halfH > grow) continue;
                    float a = 0.5f + (dx / halfW + dy / halfH) * 0.5f;
                    float b = 0.5f + (dy / halfH - dx / halfW) * 0.5f;
                    sum += SampleWrap(small, a, b);
                    inside++;
                }

                if (inside == 0) continue;
                var c = sum / inside;
                tile[x, y] = new Color(c.r, c.g, c.b, inside / (float)(ss * ss));
            }

            return tile;
        }

        private static Color SampleWrap(Img img, float u, float v)
        {
            float fx = u * img.W - 0.5f, fy = v * img.H - 0.5f;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0, ty = fy - y0;
            Color P(int x, int y) => img[((x % img.W) + img.W) % img.W, ((y % img.H) + img.H) % img.H];
            return Color.Lerp(Color.Lerp(P(x0, y0), P(x0 + 1, y0), tx), Color.Lerp(P(x0, y0 + 1), P(x0 + 1, y0 + 1), tx), ty);
        }

        // ------------------------------------------------------------------ IO

        private static string FindSource(string folder, string name)
        {
            foreach (var ext in new[] { ".png", ".jpg", ".jpeg", ".webp" })
            {
                var path = Path.Combine(folder, name + ext);
                if (File.Exists(path)) return path;
            }

            return null;
        }

        private static Img Load(string path)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(path))) throw new InvalidOperationException($"Can't read image {path}");
            var img = new Img(tex.width, tex.height);
            var pixels = tex.GetPixels();
            Array.Copy(pixels, img.P, pixels.Length);
            UnityEngine.Object.DestroyImmediate(tex);
            return img;
        }

        private static void WritePng(Img img, string assetPath)
        {
            var tex = new Texture2D(img.W, img.H, TextureFormat.RGBA32, false);
            tex.SetPixels(img.P);
            tex.Apply();
            PlaceholderArtGenerator.EnsureFolder(Path.GetDirectoryName(assetPath).Replace('\\', '/'));
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), assetPath), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void ConfigureImporter(Spec spec)
        {
            bool centred = spec.Kind == Kind.Tile || spec.Kind == Kind.Icon;
            var pivot = centred ? new Vector2(0.5f, 0.5f) : spec.Pivot;
            var mesh = spec.Kind == Kind.Building ? SpriteMeshType.Tight : SpriteMeshType.FullRect;
            PlaceholderArtGenerator.ConfigureSpriteImporter(spec.Target, pivot, mesh);
        }
    }
}
