using NUnit.Framework;
using TownBuilder.EditorTools;
using UnityEngine;

namespace TownBuilder.Tests
{
    /// <summary>v0.40.2: large building pictures put straight into the Art folder fit their plot on import.</summary>
    public sealed class DroppedArtFitterTests
    {
        private const string Coop = "Assets/_Game/Art/Buildings/Farming/PH_Building_chicken_coop.png";

        /// <summary>A 1000 px picture with faint haze everywhere and a building whose walls span x 200-799 from y 100.</summary>
        private static Color32[] HazyBuilding(out int w, out int h)
        {
            w = 1000;
            h = 1000;
            var pixels = new Color32[w * h];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(120, 120, 120, 10); // haze
            for (int y = 100; y < 900; y++)
            {
                // Walls (lower part) 200-799; a roof overhanging to 100-899 higher up.
                int l = y < 500 ? 200 : 100, r = y < 500 ? 799 : 899;
                for (int x = l; x <= r; x++) pixels[y * w + x] = new Color32(200, 90, 60, 255);
            }

            return pixels;
        }

        [Test]
        public void LargeHazyPicture_FootprintFitsThePlot_OnTheBottomCorner()
        {
            var pixels = HazyBuilding(out int w, out int h);
            Assert.IsTrue(AiArtImporter.TryMeasureDropped(Coop, pixels, w, h, out float ppu, out Vector2 pivot));
            // Walls 600 px wide = a 2x2 plot (2 cells): 300 px per cell. The haze and the roof overhang don't count.
            Assert.AreEqual(300f, ppu, 1f);
            Assert.AreEqual(0.5f, pivot.x, 0.002f, "bottom corner in the middle of the walls");
            Assert.AreEqual(0.1f, pivot.y, 0.002f, "bottom of the building, not of the picture");
        }

        [Test]
        public void ImporterSizedPicture_IsLeftAlone()
        {
            var small = new Color32[256 * 200];
            for (int i = 0; i < small.Length; i++) small[i] = new Color32(200, 90, 60, 255);
            Assert.IsFalse(AiArtImporter.TryMeasureDropped(Coop, small, 256, 200, out _, out _));
        }

        [Test]
        public void UnknownPicture_IsLeftAlone()
        {
            var pixels = HazyBuilding(out int w, out int h);
            Assert.IsFalse(AiArtImporter.TryMeasureDropped("Assets/_Game/Art/UI/Something.png", pixels, w, h, out _, out _));
        }

        [Test]
        public void Haze_IsCleared_SolidPixelsKept()
        {
            var pixels = new[] { new Color32(1, 2, 3, 10), new Color32(1, 2, 3, 0), new Color32(1, 2, 3, 40), new Color32(1, 2, 3, 255) };
            Assert.AreEqual(1, DroppedArtFitter.ClearHaze(pixels));
            Assert.AreEqual(0, pixels[0].a);
            Assert.AreEqual(40, pixels[2].a);
            Assert.AreEqual(255, pixels[3].a);
        }

        [Test]
        public void WebPWithPngName_IsRecognised()
        {
            var webp = System.Text.Encoding.ASCII.GetBytes("RIFF\0\0\0\0WEBPVP8X");
            Assert.IsTrue(DroppedArtFitter.IsWebP(webp));
            Assert.IsFalse(DroppedArtFitter.IsWebP(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 13, 10, 26, 10, 0, 0, 0, 13 }));
        }
    }
}
