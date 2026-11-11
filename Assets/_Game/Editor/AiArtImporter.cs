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
            /// <summary>Small standing prop (sign): fitted to <see cref="Spec.Width"/> pixels tall, pivot at the bottom centre.</summary>
            Prop,
            /// <summary>
            /// A straight isometric railway drawn diagonally (any length): a one-cell segment is cut along the track
            /// (a whole number of sleepers, so tiles join without seams) into a 128x64 tile along the grid Y axis.
            /// </summary>
            IsoRail,
            /// <summary>
            /// UI art (signs, buttons, ribbons, card pictures, icons): background removed, only the biggest shape kept
            /// (bits of neighbouring objects at the edges are dropped), trimmed, at most <see cref="Spec.Width"/> px.
            /// </summary>
            Ui,
        }

        private sealed class Spec
        {
            public string Source;
            public string Target;
            public Kind Kind;
            public int Width; // footprint width (Building/Flat), canvas width (Crop), square size (Icon)
            public int FootprintX = 1, FootprintY = 1;
            /// <summary>Skip the magenta-fringe cleanup (for art that is itself pink, like the pink ribbon).</summary>
            public bool NoFringeFix;
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
            new Spec("train_station", Art + "/Buildings/Community/PH_Building_train_station.png", Kind.Building, 320, 2, 3),
            // Vehicles are long diagonal shapes: measure the whole object (Flat), not the lower 40% (Building).
            new Spec("train", Art + "/Vehicles/PH_Vehicle_train.png", Kind.Flat, 384, 1, 5),
            new Spec("silo", Art + "/Buildings/Production/PH_Building_silo.png", Kind.Building, 128),
            new Spec("feed_mill", Art + "/Buildings/Production/PH_Building_feed_mill.png", Kind.Building, 256),
            new Spec("sugar_mill", Art + "/Buildings/Production/PH_Building_sugar_mill.png", Kind.Building, 256),
            new Spec("creamery", Art + "/Buildings/Production/PH_Building_creamery.png", Kind.Building, 256),
            new Spec("chicken_coop", Art + "/Buildings/Farming/PH_Building_chicken_coop.png", Kind.Building, 256),
            new Spec("dairy_shed", Art + "/Buildings/Farming/PH_Building_dairy_shed.png", Kind.Building, 256),
            new Spec("tree", Art + "/Buildings/Decorations/PH_Building_tree.png", Kind.Building, 128),
            new Spec("flower_bed", Art + "/Buildings/Decorations/PH_Building_flower_bed.png", Kind.Flat, 128),
            new Spec("bench", Art + "/Buildings/Decorations/PH_Building_bench.png", Kind.Building, 128),
            new Spec("fence", Art + "/Buildings/Decorations/PH_Building_fence.png", Kind.Building, 128),
            new Spec("fountain", Art + "/Buildings/Decorations/PH_Building_fountain.png", Kind.Building, 256),
            new Spec("scarecrow", Art + "/Buildings/Decorations/PH_Building_scarecrow.png", Kind.Building, 128),
            new Spec("festival_lantern", Art + "/Buildings/Decorations/PH_Building_festival_lantern.png", Kind.Building, 128),
            new Spec("field", Art + "/Buildings/Farming/PH_Building_field.png", Kind.Flat, 128),
            // Milestone 7 step 3: the orchard. Trees are drawn from tree.png by setup; these names replace them if supplied.
            new Spec("juice_press", Art + "/Buildings/Production/PH_Building_juice_press.png", Kind.Building, 256),
            new Spec("apple_tree", Art + "/Buildings/Farming/PH_Building_apple_tree.png", Kind.Building, 128),
            new Spec("cherry_tree", Art + "/Buildings/Farming/PH_Building_cherry_tree.png", Kind.Building, 128),
            new Spec("apple_1", Art + "/Crops/PH_Crop_apple_1.png", Kind.Building, 128),
            new Spec("apple_2", Art + "/Crops/PH_Crop_apple_2.png", Kind.Building, 128),
            new Spec("cherry_1", Art + "/Crops/PH_Crop_cherry_1.png", Kind.Building, 128),
            new Spec("cherry_2", Art + "/Crops/PH_Crop_cherry_2.png", Kind.Building, 128),
            new Spec("apple", Art + "/Products/PH_Product_apple.png", Kind.Icon, 128),
            new Spec("cherry", Art + "/Products/PH_Product_cherry.png", Kind.Icon, 128),
            new Spec("apple_juice", Art + "/Products/PH_Product_apple_juice.png", Kind.Icon, 128),
            new Spec("cherry_juice", Art + "/Products/PH_Product_cherry_juice.png", Kind.Icon, 128),
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
            new Spec("person_1", Art + "/Characters/PH_Person_1.png", Kind.Prop, 64),
            new Spec("person_2", Art + "/Characters/PH_Person_2.png", Kind.Prop, 64),
            new Spec("person_3", Art + "/Characters/PH_Person_3.png", Kind.Prop, 64),
            new Spec("person_4", Art + "/Characters/PH_Person_4.png", Kind.Prop, 64),
            // Car: 1.3 x 0.7 cells along X; footprint 13x7 only sets the pivot ratio (bottom corner at 7/20 across).
            new Spec("car_front", Art + "/Vehicles/PH_Vehicle_car_front.png", Kind.Flat, 128, 13, 7),
            new Spec("car_back", Art + "/Vehicles/PH_Vehicle_car_back.png", Kind.Flat, 128, 13, 7),
            new Spec("coin", Art + "/UI/PH_Icon_Coin.png", Kind.Icon, 64),
            new Spec("gem", Art + "/UI/PH_Icon_Gem.png", Kind.Icon, 64),
            new Spec("xp_star", Art + "/UI/PH_Icon_Xp.png", Kind.Icon, 64),
            new Spec("for_sale_sign", Art + "/Environment/PH_Sign_ForSale.png", Kind.Prop, 96),
            new Spec("rail", Art + "/Environment/Tiles/PH_Tile_Rail.png", Kind.IsoRail, 128),
            new Spec("grass", Art + "/Environment/Tiles/PH_Tile_Grass.png", Kind.Tile, 128),
            new Spec("sand", Art + "/Environment/Tiles/PH_Tile_Sand.png", Kind.Tile, 128),
            new Spec("water", Art + "/Environment/Tiles/PH_Tile_Water.png", Kind.Tile, 128),
            new Spec("road", Art + "/Environment/Tiles/PH_Tile_Road.png", Kind.Tile, 128),

            // v0.27.4: Build window and top bar art.
            new Spec("build_sign", Art + "/UI/Build/UI_BuildSign.png", Kind.Ui, 1024),
            new Spec("close_button", Art + "/UI/Build/UI_CloseButton.png", Kind.Ui, 256),
            new Spec("corner_leaves", Art + "/UI/Build/UI_CornerLeaves.png", Kind.Ui, 256),
            new Spec("ribbon_blue", Art + "/UI/Build/UI_Ribbon_Blue.png", Kind.Ui, 400),
            new Spec("ribbon_yellow", Art + "/UI/Build/UI_Ribbon_Yellow.png", Kind.Ui, 400),
            new Spec("ribbon_orange", Art + "/UI/Build/UI_Ribbon_Orange.png", Kind.Ui, 400),
            new Spec("ribbon_green", Art + "/UI/Build/UI_Ribbon_Green.png", Kind.Ui, 400),
            new Spec("ribbon_pink", Art + "/UI/Build/UI_Ribbon_Pink.png", Kind.Ui, 400) { NoFringeFix = true },
            new Spec("card_houses", Art + "/UI/Build/UI_Card_Houses.png", Kind.Ui, 400),
            new Spec("card_town", Art + "/UI/Build/UI_Card_Town.png", Kind.Ui, 400),
            new Spec("card_factories", Art + "/UI/Build/UI_Card_Factories.png", Kind.Ui, 400),
            new Spec("card_farm", Art + "/UI/Build/UI_Card_Farm.png", Kind.Ui, 400),
            new Spec("card_decorations", Art + "/UI/Build/UI_Card_Decorations.png", Kind.Ui, 400),
            new Spec("icon_level", Art + "/UI/PH_Icon_Level.png", Kind.Ui, 160),
            new Spec("icon_barn", Art + "/UI/PH_Icon_Barn.png", Kind.Ui, 160),
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
            if (spec.Kind == Kind.IsoRail) return MakeRailTile(KeyBackground(src), spec.Width);

            var keyed = KeyBackground(src, !spec.NoFringeFix);
            if (spec.Kind == Kind.Ui) KeepLargestShape(keyed);
            if (!FindBounds(keyed, out int minX, out int maxX, out int minY, out int maxY))
                throw new InvalidOperationException($"{spec.Source}: no object found on the background");

            switch (spec.Kind)
            {
                case Kind.Prop:
                {
                    float scale = spec.Width / (float)(maxY - minY + 1);
                    float cx = (minX + maxX + 1) * 0.5f;
                    int outW = Mathf.CeilToInt((maxX - minX + 1) * scale) + 2;
                    float srcW = outW / scale;
                    spec.Pivot = new Vector2(0.5f, 0f);
                    return Resample(keyed, cx - srcW * 0.5f, minY, srcW, spec.Width / scale, outW, spec.Width);
                }

                case Kind.Ui:
                {
                    const int pad = 2;
                    float w = maxX - minX + 1 + 2 * pad, h = maxY - minY + 1 + 2 * pad;
                    float scale = Mathf.Min(1f, spec.Width / Mathf.Max(w, h));
                    int outW = Mathf.Max(1, Mathf.RoundToInt(w * scale)), outH = Mathf.Max(1, Mathf.RoundToInt(h * scale));
                    return Resample(keyed, minX - pad, minY - pad, w, h, outW, outH);
                }

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
        private static Img KeyBackground(Img src, bool fixFringe = true)
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

            if (!fixFringe) return dst;

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

        /// <summary>Clears every opaque region except the biggest one (4-connected, alpha &gt;= 0.5 seeds).</summary>
        private static void KeepLargestShape(Img img)
        {
            var label = new int[img.P.Length];
            var sizes = new List<int> { 0 };
            var stack = new Stack<int>();
            for (int i = 0; i < img.P.Length; i++)
            {
                if (label[i] != 0 || img.P[i].a < 0.5f) continue;
                int id = sizes.Count, count = 0;
                label[i] = id;
                stack.Push(i);
                while (stack.Count > 0)
                {
                    int p = stack.Pop();
                    count++;
                    int x = p % img.W, y = p / img.W;
                    void Visit(int q)
                    {
                        if (label[q] != 0 || img.P[q].a < 0.5f) return;
                        label[q] = id;
                        stack.Push(q);
                    }

                    if (x > 0) Visit(p - 1);
                    if (x < img.W - 1) Visit(p + 1);
                    if (y > 0) Visit(p - img.W);
                    if (y < img.H - 1) Visit(p + img.W);
                }

                sizes.Add(count);
            }

            int best = 0;
            for (int k = 1; k < sizes.Count; k++)
                if (sizes[k] > sizes[best]) best = k;

            // Keep the biggest shape plus its soft edge (semi-transparent pixels next to it).
            for (int y = 0; y < img.H; y++)
            for (int x = 0; x < img.W; x++)
            {
                int i = y * img.W + x;
                if (label[i] == best) continue;
                if (label[i] == 0)
                {
                    bool nearBest = false;
                    for (int oy = -2; oy <= 2 && !nearBest; oy++)
                    for (int ox = -2; ox <= 2 && !nearBest; ox++)
                    {
                        int nx = x + ox, ny = y + oy;
                        nearBest = nx >= 0 && ny >= 0 && nx < img.W && ny < img.H && label[ny * img.W + nx] == best;
                    }

                    if (nearBest) continue;
                }

                img.P[i] = new Color(0f, 0f, 0f, 0f);
            }
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
        /// <summary>Fraction of a cell's width the railway bed (gravel + sleepers) should cover.</summary>
        private const float RailBandShare = 0.62f;

        private static Img MakeRailTile(Img src, int width)
        {
            // 1) Track direction = principal axis of the opaque pixels.
            double sx = 0, sy = 0, n = 0;
            for (int y = 0; y < src.H; y += 2)
            for (int x = 0; x < src.W; x += 2)
                if (src[x, y].a > 0.5f) { sx += x; sy += y; n++; }
            if (n < 10) throw new InvalidOperationException("rail: no track found on the background");
            double mx = sx / n, my = sy / n, cxx = 0, cyy = 0, cxy = 0;
            for (int y = 0; y < src.H; y += 2)
            for (int x = 0; x < src.W; x += 2)
            {
                if (src[x, y].a <= 0.5f) continue;
                double dx = x - mx, dy = y - my;
                cxx += dx * dx; cyy += dy * dy; cxy += dx * dy;
            }

            double angle = 0.5 * Math.Atan2(2 * cxy, cxx - cyy);
            var along = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
            if (along.x > 0f) along = -along; // grid +Y on screen = up-left (texture rows go up)
            var across = new Vector2(-along.x, along.y); // the other isometric axis (mirror image)
            var centre = new Vector2((float)mx, (float)my);

            // 2) Width of the track bed along the "across" axis, measured at the centre.
            float Extent(Vector2 dir)
            {
                int gap = 0;
                float last = 0f;
                for (float t = 0; t < src.W; t += 1f)
                {
                    var p = centre + dir * t;
                    int x = Mathf.RoundToInt(p.x), y = Mathf.RoundToInt(p.y);
                    if (x < 0 || y < 0 || x >= src.W || y >= src.H) break;
                    if (src[x, y].a > 0.5f) { last = t; gap = 0; }
                    else if (++gap > 6) break;
                }

                return last;
            }

            float plus = Extent(across), minus = Extent(-across);
            centre += across * ((plus - minus) * 0.5f);
            float cell = (plus + minus) / RailBandShare;

            // 3) Sleeper period along the track (autocorrelation of brightness on a line off the centre), so one tile
            //    holds a whole number of sleepers and neighbouring tiles join seamlessly.
            var offset = across * ((plus + minus) * 0.12f);
            float span = cell * 2.5f;
            int samples = Mathf.Max(64, (int)span);
            var lum = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                var p = centre + offset + along * (i - samples * 0.5f);
                int x = Mathf.Clamp(Mathf.RoundToInt(p.x), 0, src.W - 1), y = Mathf.Clamp(Mathf.RoundToInt(p.y), 0, src.H - 1);
                var c = src[x, y];
                lum[i] = c.a > 0.5f ? c.r * 0.3f + c.g * 0.59f + c.b * 0.11f : 0f;
            }

            float mean = 0f;
            foreach (var v in lum) mean += v;
            mean /= samples;
            int bestLag = 0;
            float best = float.MinValue;
            for (int lag = Mathf.Max(8, (int)(cell * 0.1f)); lag < cell * 0.6f && lag < samples / 2; lag++)
            {
                float s = 0f;
                for (int i = 0; i + lag < samples; i++) s += (lum[i] - mean) * (lum[i + lag] - mean);
                s /= samples - lag;
                if (s > best) { best = s; bestLag = lag; }
            }

            float alongLength = bestLag > 0 ? Mathf.Max(1, Mathf.Round(cell / bestLag)) * bestLag : cell;

            // 4) Sample the diamond: tile pixel → grid (gx across, gy along) → source point.
            int height = width / 2;
            var tile = new Img(width, height);
            const int ss = 3;
            float halfW = width * 0.5f, halfH = height * 0.5f;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var sum = Color.clear;
                int inside = 0;
                for (int sy2 = 0; sy2 < ss; sy2++)
                for (int sx2 = 0; sx2 < ss; sx2++)
                {
                    float dx = (x + (sx2 + 0.5f) / ss - halfW) / halfW, dy = (y + (sy2 + 0.5f) / ss - halfH) / halfH;
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) > 1f + 1.5f / halfW) continue;
                    float gx = (dx + dy) * 0.5f, gy = (dy - dx) * 0.5f;
                    var p = centre + across * (gx * cell) + along * (gy * alongLength);
                    sum += Bilinear(src, p.x, p.y);
                    inside++;
                }

                if (inside == 0) continue;
                var c = sum / (ss * ss);
                tile[x, y] = c.a > 0.001f ? new Color(c.r / c.a, c.g / c.a, c.b / c.a, c.a) : Color.clear;
            }

            Debug.Log($"[AiArt] rail: direction {along}, bed {plus + minus:0} px, cell {cell:0} px, sleeper period {bestLag} px, segment {alongLength:0} px");
            return tile;
        }

        /// <summary>Premultiplied bilinear sample (transparent outside the image).</summary>
        private static Color Bilinear(Img img, float fx, float fy)
        {
            fx -= 0.5f;
            fy -= 0.5f;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0, ty = fy - y0;
            Color P(int x, int y) => Premul(img, x, Mathf.Clamp(y, 0, img.H - 1)) * (y < 0 || y >= img.H ? 0f : 1f);
            return Color.Lerp(Color.Lerp(P(x0, y0), P(x0 + 1, y0), tx), Color.Lerp(P(x0, y0 + 1), P(x0 + 1, y0 + 1), tx), ty);
        }

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
            bool centred = spec.Kind == Kind.Tile || spec.Kind == Kind.Icon || spec.Kind == Kind.Ui;
            var pivot = centred ? new Vector2(0.5f, 0.5f) : spec.Pivot;
            var mesh = spec.Kind == Kind.Building ? SpriteMeshType.Tight : SpriteMeshType.FullRect;
            PlaceholderArtGenerator.ConfigureSpriteImporter(spec.Target, pivot, mesh);
        }
    }
}
