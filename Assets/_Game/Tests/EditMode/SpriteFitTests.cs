using NUnit.Framework;
using TownBuilder.Domain;
using TownBuilder.EditorTools;
using UnityEditor;
using UnityEngine;

namespace TownBuilder.Tests
{
    /// <summary>v0.40.2: a picture much wider than its footprint is drawn fitted to its plot.</summary>
    public sealed class SpriteFitTests
    {
        /// <summary>A picture with transparent margins and a visible block of the given size (pixels).</summary>
        private static Sprite MakeSprite(int width, int height, RectInt visible, float ppu = 128f)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            for (int y = visible.yMin; y < visible.yMax; y++)
            for (int x = visible.xMin; x < visible.xMax; x++)
                pixels[y * width + x] = new Color32(200, 120, 40, 255);
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0f), ppu, 0, SpriteMeshType.Tight);
        }

        [Test]
        public void LargePicture_IsFittedToTheFootprintWidth_OnThePivot()
        {
            // 1000 px of visible picture = 7.8 cells at 128 px per cell, on a 2x2 plot (2 cells wide).
            var big = MakeSprite(1200, 600, new RectInt(100, 50, 1000, 500));
            var fitted = SpriteFit.ForFootprint(big, new GridCoord(2, 2));
            Assert.AreNotSame(big, fitted);

            var visible = SpriteFit.VisibleUnits(fitted);
            Assert.AreEqual(2f, visible.width, 0.06f, "as wide as the plot");
            Assert.AreEqual(0f, visible.yMin, 0.03f, "the bottom of the picture sits on the plot's bottom corner");
            Assert.AreEqual(0f, visible.center.x, 0.03f, "centred on the plot");
            Assert.AreEqual(big.rect, fitted.rect, "the whole picture is kept, so effect points still match");
        }

        [Test]
        public void PictureThatFits_IsUnchanged()
        {
            var fits = MakeSprite(256, 200, new RectInt(0, 0, 256, 200));
            Assert.AreSame(fits, SpriteFit.ForFootprint(fits, new GridCoord(2, 2)));
            var slightlyWide = MakeSprite(280, 200, new RectInt(0, 0, 280, 200)); // roof overhang
            Assert.AreSame(slightlyWide, SpriteFit.ForFootprint(slightlyWide, new GridCoord(2, 2)));
        }

        [Test]
        public void SameFitIsReused_AndTurningKeepsIt()
        {
            var big = MakeSprite(800, 400, new RectInt(0, 0, 800, 400));
            var a = SpriteFit.ForFootprint(big, new GridCoord(2, 3));
            Assert.AreSame(a, SpriteFit.ForFootprint(big, new GridCoord(2, 3)));
            Assert.AreSame(a, SpriteFit.ForFootprint(big, new GridCoord(3, 2)), "turned footprint: same width");
            Assert.AreEqual(2.5f, SpriteFit.VisibleUnits(a).width, 0.06f);
        }

        [Test]
        public void EveryBuilding_IsDrawnNoWiderThanItsPlot()
        {
            var content = AssetDatabase.LoadAssetAtPath<GameContent>(ProjectSetup.ContentPath);
            Assert.NotNull(content);
            foreach (var building in content.Buildings)
            {
                if (building == null || building.Sprite == null) continue;
                // Large pictures put straight into the Art folder are sized on import (DroppedArtFitter): walls, not eaves.
                if (!Mathf.Approximately(building.Sprite.pixelsPerUnit, ArtConventions.PixelsPerUnit)) continue;
                var drawn = SpriteFit.ForFootprint(building.Sprite, building.FootprintSize);
                float limit = SpriteFit.FootprintWidth(building.FootprintSize) * SpriteFit.Tolerance + 0.02f;
                Assert.LessOrEqual(SpriteFit.VisibleUnits(drawn).width, limit, building.Id);
            }
        }

        [Test]
        public void NoBigBuilding_IsDrawnTiny()
        {
            // v0.41.1: a normal picture that kept a large picture's fit was drawn ~6x too small on phones.
            var content = AssetDatabase.LoadAssetAtPath<GameContent>(ProjectSetup.ContentPath);
            foreach (var building in content.Buildings)
            {
                if (building == null || building.Sprite == null) continue;
                var size = building.FootprintSize;
                if (size.X + size.Y < 4) continue; // 2x2 and bigger
                float width = SpriteFit.VisibleUnits(SpriteFit.ForFootprint(building.Sprite, size)).width;
                Assert.GreaterOrEqual(width, SpriteFit.FootprintWidth(size) * 0.6f,
                    $"{building.Id} is drawn {width:0.00} cells wide on a {SpriteFit.FootprintWidth(size):0.#}-cell plot "
                    + $"({building.Sprite.pixelsPerUnit:0} px per cell)");
            }
        }

        [Test]
        public void StandardSettings_ForANormalSizePicture()
        {
            Assert.IsTrue(AiArtImporter.TryStandardSettings("Assets/_Game/Art/Buildings/Production/PH_Building_bakery.png", 292, 262,
                out float ppu, out Vector2 pivot));
            Assert.AreEqual(128f, ppu);
            Assert.AreEqual(new Vector2(0.5f, 0f), pivot);
            Assert.IsFalse(AiArtImporter.TryStandardSettings("Assets/_Game/Art/UI/Other.png", 100, 100, out _, out _));
        }
    }
}
