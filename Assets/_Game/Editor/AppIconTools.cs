using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace TownBuilder.EditorTools
{
    /// <summary>
    /// The app icon (v0.35.0). <see cref="IconPath"/> is a square 1024x1024 picture: either the owner's AI image
    /// (Sources/app_icon.png, imported by <see cref="ImportFromSource"/>) or a placeholder drawn here. It becomes the
    /// default icon and the background layer of the Android adaptive icon (the launcher shows
    /// roughly the middle two thirds, so the subject must sit in the centre). The foreground layer is transparent.
    /// </summary>
    public static class AppIconTools
    {
        public const string Folder = "Assets/_Game/Art/AppIcon";
        public const string IconPath = Folder + "/AppIcon.png";
        public const string ForegroundPath = Folder + "/AppIcon_Foreground.png";
        public const string SourceFileName = "app_icon.png";
        public const int Size = 1024;

        /// <summary>Draws the placeholder icon if there is no icon yet, then sets it in Player Settings.</summary>
        public static void EnsureAndApply()
        {
            PlaceholderArtGenerator.EnsureFolder(Folder);
            if (!File.Exists(IconPath))
                WriteIcon(RenderPlaceholder());
            if (!File.Exists(ForegroundPath))
            {
                var clear = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                clear.SetPixels32(new Color32[Size * Size]);
                File.WriteAllBytes(ForegroundPath, clear.EncodeToPNG());
                Object.DestroyImmediate(clear);
                ImportIconTexture(ForegroundPath);
            }

            Apply();
        }

        /// <summary>Uses the owner's square picture (any size): centre-cropped to a square and scaled to 1024x1024.</summary>
        public static bool ImportFromSource(string sourceFolder)
        {
            var path = Path.Combine(sourceFolder, SourceFileName);
            if (!File.Exists(path)) return false;
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!src.LoadImage(File.ReadAllBytes(path)))
            {
                Debug.LogWarning($"[AppIcon] Could not read {path}");
                return false;
            }

            int side = Mathf.Min(src.width, src.height);
            int ox = (src.width - side) / 2, oy = (src.height - side) / 2;
            var dst = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var px = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                var c = src.GetPixelBilinear((ox + (x + 0.5f) * side / Size) / src.width, (oy + (y + 0.5f) * side / Size) / src.height);
                c.a = 1f; // launcher icons are opaque squares
                px[y * Size + x] = c;
            }

            dst.SetPixels32(px);
            dst.Apply();
            Object.DestroyImmediate(src);
            PlaceholderArtGenerator.EnsureFolder(Folder);
            WriteIcon(dst);
            Debug.Log($"[AppIcon] Imported {path} ({side}x{side} centre) as the app icon.");
            return true;
        }

        private static void WriteIcon(Texture2D tex)
        {
            File.WriteAllBytes(IconPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            ImportIconTexture(IconPath);
        }

        private static void ImportIconTexture(string assetPath)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = Size;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        /// <summary>Sets the default icon and the Android adaptive icon.</summary>
        public static void Apply()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            var foreground = AssetDatabase.LoadAssetAtPath<Texture2D>(ForegroundPath);
            if (icon == null) return;

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            // Unity 6.6 only has adaptive icons on Android (fine: our minimum is Android 8.0, which introduced them).
            var android = NamedBuildTarget.Android;
            var adaptive = PlayerSettings.GetPlatformIcons(android, AndroidPlatformIconKind.Adaptive);
            foreach (var slot in adaptive) slot.SetTextures(icon, foreground); // layer 0 = background, 1 = foreground
            PlayerSettings.SetPlatformIcons(android, AndroidPlatformIconKind.Adaptive, adaptive);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Placeholder until the owner's icon arrives: a sunny sky, a round green hill, a cream cottage with a red roof and
        /// a little tree, all inside the middle of the square (adaptive-icon safe zone).
        /// </summary>
        private static Texture2D RenderPlaceholder()
        {
            const int n = Size;
            var px = new Color32[n * n];
            var skyTop = new Color(0.36f, 0.70f, 0.95f);
            var skyLow = new Color(0.78f, 0.92f, 1f);
            for (int y = 0; y < n; y++)
            {
                var c = (Color32)Color.Lerp(skyLow, skyTop, (float)y / n);
                for (int x = 0; x < n; x++) px[y * n + x] = c;
            }

            void Disc(float cx, float cy, float r, Color32 c)
            {
                int x0 = Mathf.Max(0, (int)(cx - r - 1)), x1 = Mathf.Min(n - 1, (int)(cx + r + 1));
                int y0 = Mathf.Max(0, (int)(cy - r - 1)), y1 = Mathf.Min(n - 1, (int)(cy + r + 1));
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    if (a > 0f) px[y * n + x] = Color32.Lerp(px[y * n + x], c, a);
                }
            }

            void Box(float x0, float y0, float x1, float y1, Color32 c)
            {
                for (int y = Mathf.Max(0, (int)y0); y < Mathf.Min(n, (int)y1); y++)
                for (int x = Mathf.Max(0, (int)x0); x < Mathf.Min(n, (int)x1); x++)
                    px[y * n + x] = c;
            }

            void Roof(float left, float right, float baseY, float peakY, Color32 c)
            {
                float mid = (left + right) * 0.5f;
                for (int y = Mathf.Max(0, (int)baseY); y < Mathf.Min(n, (int)peakY); y++)
                {
                    float t = (y - baseY) / (peakY - baseY);
                    float half = (right - left) * 0.5f * (1f - t);
                    for (int x = Mathf.Max(0, (int)(mid - half)); x < Mathf.Min(n, (int)(mid + half)); x++)
                        px[y * n + x] = c;
                }
            }

            // Sun, clouds
            Disc(330, 700, 86, new Color32(255, 214, 80, 255));
            Disc(330, 700, 64, new Color32(255, 232, 120, 255));
            Disc(640, 690, 44, new Color32(255, 255, 255, 235));
            Disc(690, 700, 56, new Color32(255, 255, 255, 235));
            Disc(745, 688, 40, new Color32(255, 255, 255, 235));

            // Hills: a far darker one, then the big near hill covering the bottom.
            Disc(250, -260, 640, new Color32(96, 168, 76, 255));
            Disc(800, -420, 820, new Color32(120, 194, 88, 255));

            // Cottage (centre)
            Box(400, 330, 640, 520, new Color32(70, 46, 30, 255));          // outline
            Box(408, 338, 632, 512, new Color32(250, 236, 205, 255));       // walls
            Roof(368, 672, 500, 660, new Color32(70, 46, 30, 255));
            Roof(384, 656, 508, 646, new Color32(214, 74, 58, 255));        // red roof
            Box(486, 338, 554, 440, new Color32(124, 78, 44, 255));         // door
            Disc(540, 392, 6, new Color32(255, 214, 80, 255));              // knob
            Box(424, 410, 474, 462, new Color32(70, 46, 30, 255));
            Box(430, 416, 468, 456, new Color32(170, 220, 245, 255));       // window
            Box(568, 410, 618, 462, new Color32(70, 46, 30, 255));
            Box(574, 416, 612, 456, new Color32(170, 220, 245, 255));       // window
            Box(580, 600, 616, 680, new Color32(150, 92, 60, 255));         // chimney

            // Tree to the right
            Box(718, 330, 744, 420, new Color32(120, 78, 44, 255));
            Disc(731, 452, 62, new Color32(58, 128, 56, 255));
            Disc(731, 462, 52, new Color32(82, 160, 70, 255));

            // Path to the door
            for (int y = 200; y < 338; y++)
            {
                float t = (338f - y) / 138f;
                float half = 34f + t * 40f;
                for (int x = (int)(520 - half); x < (int)(520 + half); x++)
                    px[y * n + x] = new Color32(222, 196, 140, 255);
            }

            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
