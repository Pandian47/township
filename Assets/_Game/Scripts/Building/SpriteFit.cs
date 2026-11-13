using System.Collections.Generic;
using TownBuilder.Domain;
using UnityEngine;

namespace TownBuilder
{
    /// <summary>
    /// v0.40.2: a building picture much wider than its footprint (for example a large image put straight into the Art
    /// folder without the AI art importer) is shown fitted to its plot. It is the same texture, drawn smaller so the
    /// visible part is exactly as wide as the footprint diamond, with the bottom of the visible part on the footprint's
    /// bottom corner (the pivot). Pictures that already fit are returned unchanged.
    /// The fitted sprite keeps the whole picture rectangle, so effect points measured 0-1 on the picture still match.
    /// </summary>
    public static class SpriteFit
    {
        /// <summary>A picture up to this much wider than its footprint is left alone (roof overhangs, signs, trees).</summary>
        public const float Tolerance = 1.15f;

        private static readonly Dictionary<(Sprite sprite, int width), Sprite> Cache = new Dictionary<(Sprite, int), Sprite>();

        /// <summary>The visible part of each fitted sprite (its own mesh is the full rectangle), in pixels.</summary>
        private static readonly Dictionary<Sprite, Rect> FittedVisible = new Dictionary<Sprite, Rect>();

        /// <summary>World width of an X x Y footprint's diamond: (X + Y) / 2 cells, one cell being 1 unit wide.</summary>
        public static float FootprintWidth(GridCoord footprint) => (footprint.X + footprint.Y) * 0.5f;

        /// <summary>The sprite to draw for a building with this footprint (cached, so repeated calls return the same sprite).</summary>
        public static Sprite ForFootprint(Sprite sprite, GridCoord footprint)
        {
            if (sprite == null) return null;
            var key = (sprite, footprint.X + footprint.Y); // turning swaps X and Y: same width
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            var result = Fit(sprite, FootprintWidth(footprint));
            Cache[key] = result;
            return result;
        }

        /// <summary>The visible part of the picture in world units (its width and how far it reaches above the pivot).</summary>
        public static Rect VisibleUnits(Sprite sprite)
        {
            var px = VisiblePixels(sprite);
            float ppu = sprite.pixelsPerUnit;
            return new Rect((px.xMin - sprite.pivot.x) / ppu, (px.yMin - sprite.pivot.y) / ppu, px.width / ppu, px.height / ppu);
        }

        private static Sprite Fit(Sprite sprite, float targetWidth)
        {
            if (sprite.packed || sprite.texture == null) return sprite;
            // Not the standard 128 px per cell: already sized on purpose (the Art folder fits large pictures on import).
            if (!Mathf.Approximately(sprite.pixelsPerUnit, ArtConventions.PixelsPerUnit)) return sprite;
            var visible = VisiblePixels(sprite);
            if (visible.width <= 0f || visible.width / sprite.pixelsPerUnit <= targetWidth * Tolerance) return sprite;

            var rect = sprite.rect;
            var pivot = new Vector2(visible.center.x / rect.width, visible.yMin / rect.height);
            var fitted = Sprite.Create(sprite.texture, rect, pivot, visible.width / targetWidth, 0, SpriteMeshType.FullRect);
            fitted.name = sprite.name + " (fitted)";
            FittedVisible[fitted] = visible;
            Debug.Log($"[SpriteFit] '{sprite.name}' is {visible.width / sprite.pixelsPerUnit:0.#} cells wide for a "
                      + $"{targetWidth:0.#}-cell plot: drawn {visible.width / sprite.pixelsPerUnit / targetWidth:0.#}x smaller. "
                      + "Import it with the AI art importer for a sharper, lighter picture.");
            return fitted;
        }

        /// <summary>
        /// The visible (non-transparent) part in pixels from the picture's bottom-left, read from the sprite's mesh: a
        /// Tight mesh outlines the visible pixels, a Full Rect mesh is the whole picture.
        /// </summary>
        private static Rect VisiblePixels(Sprite sprite)
        {
            if (FittedVisible.TryGetValue(sprite, out var known)) return known;
            var vertices = sprite.vertices; // world units, relative to the pivot
            if (vertices == null || vertices.Length == 0) return new Rect(0f, 0f, sprite.rect.width, sprite.rect.height);
            float ppu = sprite.pixelsPerUnit;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (var v in vertices)
            {
                min = Vector2.Min(min, v);
                max = Vector2.Max(max, v);
            }

            var p = sprite.pivot; // pixels
            return Rect.MinMaxRect(
                Mathf.Max(0f, min.x * ppu + p.x), Mathf.Max(0f, min.y * ppu + p.y),
                Mathf.Min(sprite.rect.width, max.x * ppu + p.x), Mathf.Min(sprite.rect.height, max.y * ppu + p.y));
        }
    }
}
