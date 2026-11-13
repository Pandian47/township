using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TownBuilder.EditorTools
{
    /// <summary>
    /// v0.40.2: building pictures put straight into the Art folder (any size, not through the AI art importer) fit their
    /// plot automatically. On import:
    /// <list type="bullet">
    /// <item>pixels per unit and pivot are set so the footprint is as wide as the plot and its bottom corner sits on the
    /// plot's bottom corner (measured like the importer: the lower walls, ignoring faint background haze);</item>
    /// <item>the faint haze some AI tools leave around the picture (alpha below <see cref="HazeAlpha"/>) is cleared;</item>
    /// <item>the texture is capped at <see cref="MaxSize"/> px to save phone memory.</item>
    /// </list>
    /// The picture file itself is never changed. A WebP picture saved with a .png name gets a clear error.
    /// </summary>
    public sealed class DroppedArtFitter : AssetPostprocessor
    {
        /// <summary>Pixels more transparent than this are background haze and become fully transparent.</summary>
        public const byte HazeAlpha = 32;

        /// <summary>Largest texture size for a fitted picture (a 2x2 building is at most ~700 px on screen).</summary>
        public const int MaxSize = 1024;

        private const string ArtFolder = "Assets/_Game/Art/";
        private static readonly HashSet<string> Fitted = new HashSet<string>();

        public override uint GetVersion() => 2;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtFolder) || !assetPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)) return;
            if (!File.Exists(assetPath)) return;
            var bytes = File.ReadAllBytes(assetPath);
            if (IsWebP(bytes))
            {
                Debug.LogError($"[Art] {assetPath} is a WebP picture saved with a .png name, so Unity can't read it. "
                               + "Open it in Paint and use Save as > PNG.");
                return;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(bytes)) return;
                var importer = (TextureImporter)assetImporter;
                if (!AiArtImporter.TryMeasureDropped(assetPath, texture.GetPixels32(), texture.width, texture.height,
                        out float ppu, out Vector2 pivot))
                {
                    // v0.41.1: a normal-size picture that replaced a large one must not keep the large one's fit
                    // (it would be drawn several times too small).
                    if (!Mathf.Approximately(importer.spritePixelsPerUnit, ArtConventions.PixelsPerUnit)
                        && AiArtImporter.TryStandardSettings(assetPath, texture.width, texture.height, out var stdPpu, out var stdPivot))
                    {
                        var reset = new TextureImporterSettings();
                        importer.ReadTextureSettings(reset);
                        reset.spriteAlignment = (int)SpriteAlignment.Custom;
                        reset.spritePivot = stdPivot;
                        reset.spritePixelsPerUnit = stdPpu;
                        importer.SetTextureSettings(reset);
                        Debug.Log($"[Art] {Path.GetFileName(assetPath)} is a normal-size picture again: back to 128 px per cell.");
                    }

                    return;
                }

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot;
                settings.spritePixelsPerUnit = ppu;
                settings.spriteMeshType = SpriteMeshType.Tight;
                importer.SetTextureSettings(settings);
                importer.maxTextureSize = Mathf.Min(importer.maxTextureSize, MaxSize);
                Fitted.Add(assetPath);
                Debug.Log($"[Art] {Path.GetFileName(assetPath)} ({texture.width}x{texture.height}) fitted to its plot: "
                          + $"{ppu:0} px per cell, bottom corner at ({pivot.x:0.000}, {pivot.y:0.000}).");
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        private void OnPostprocessTexture(Texture2D texture)
        {
            if (!Fitted.Remove(assetPath)) return;
            for (int mip = 0; mip < texture.mipmapCount; mip++)
            {
                var pixels = texture.GetPixels32(mip);
                ClearHaze(pixels);
                texture.SetPixels32(pixels, mip);
            }

            texture.Apply(false);
        }

        /// <summary>Makes pixels fainter than <see cref="HazeAlpha"/> fully transparent.</summary>
        public static int ClearHaze(Color32[] pixels)
        {
            int cleared = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a >= HazeAlpha || pixels[i].a == 0) continue;
                pixels[i] = new Color32(0, 0, 0, 0);
                cleared++;
            }

            return cleared;
        }

        /// <summary>RIFF....WEBP header.</summary>
        public static bool IsWebP(byte[] bytes) =>
            bytes != null && bytes.Length >= 12 && bytes[0] == 'R' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == 'F'
            && bytes[8] == 'W' && bytes[9] == 'E' && bytes[10] == 'B' && bytes[11] == 'P';

        /// <summary>Re-imports every building picture so the fit is applied (menu TownBuilder > Art).</summary>
        [MenuItem("TownBuilder/Art/Refit Building Pictures")]
        public static void RefitAll()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Game/Art/Buildings", "Assets/_Game/Art/Vehicles" }))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
        }
    }
}
