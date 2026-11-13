using UnityEngine;

namespace TownBuilder
{
    /// <summary>
    /// Pictures drawn in code (no art files) for edit mode: the yellow up arrow shown above a building while a finger is held
    /// on it, the lime corner arrows at the sides of a picked-up building, and the soft diamond shadow under it.
    /// Drawn with signed distance shapes, so the edges are smooth at any size.
    /// </summary>
    public static class CueSprites
    {
        private static Sprite _corner;
        private static Sprite _up;
        private static Sprite _shadow;

        /// <summary>Width of <see cref="CornerArrow"/> in world units at scale 1.</summary>
        public const float CornerArrowWidth = 0.72f;

        /// <summary>Width and height of <see cref="Shadow"/> in world units at scale 1.</summary>
        public static readonly Vector2 ShadowSize = new Vector2(2.56f, 1.28f);

        /// <summary>
        /// A right triangle: upright edge on the left, flat bottom, the slanted edge ends at the bottom right (parallel to the
        /// lower-left edge of an isometric footprint). Pivot = its top-left corner. White-ish: tint it. Flip it for the right side.
        /// </summary>
        public static Sprite CornerArrow => _corner != null ? _corner : (_corner = MakeCorner());

        /// <summary>A banded yellow arrow with a brown outline pointing up, pivot at the bottom centre (160 px per unit).</summary>
        public static Sprite UpArrow => _up != null ? _up : (_up = MakeUp());

        /// <summary>A solid isometric diamond with softened edges, pivot in the middle. Tint it.</summary>
        public static Sprite Shadow => _shadow != null ? _shadow : (_shadow = MakeShadow());

        // ------------------------------------------------------------------ Corner arrow

        private static Sprite MakeCorner()
        {
            const int w = 72, h = 36;
            const float r = 4f;
            var tri = InsetTriangle(new Vector2(0, 0), new Vector2(w, 0), new Vector2(0, h), r);
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float d = SdPolygon(new Vector2(x + 0.5f, y + 0.5f), tri) - r;
                    float a = Mathf.Clamp01(0.7f - d);
                    if (a <= 0f) continue;
                    float t = y / (float)h;
                    var c = Color.Lerp(new Color(0.72f, 1f, 0.1f), new Color(0.88f, 1f, 0.45f), t * 0.7f);
                    pixels[y * w + x] = new Color(c.r, c.g, c.b, a);
                }
            }

            return ToSprite(pixels, w, h, new Vector2(0f, 1f), 100f);
        }

        // ------------------------------------------------------------------ Up arrow

        private static Sprite MakeUp()
        {
            const int w = 112, h = 176;
            const int headBase = 104;
            const float r = 9f;
            var outline = new Color(0.55f, 0.3f, 0.04f);
            var tri = InsetTriangle(new Vector2(2, headBase), new Vector2(w - 2, headBase), new Vector2(w * 0.5f, h - 2), r);
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float d = Mathf.Min(SdPolygon(p, tri) - r, SdRoundBox(p, new Vector2(w * 0.5f, 62f), new Vector2(25f, 58f), 14f));
                    float a = Mathf.Clamp01(0.7f - d);
                    if (a <= 0f) continue;

                    float t = y / (float)h;
                    var c = new Color(0.98f + 0.02f * t, 0.8f + 0.17f * t, 0.06f + 0.3f * t);
                    if (y < headBase - 4)
                    {
                        // The shaft is five rounded segments: a lighter top half and a brown line between each.
                        float local = (y - 6) % 22f;
                        if (local > 13.6f) c = new Color(Mathf.Min(1f, c.r + 0.1f), Mathf.Min(1f, c.g + 0.1f), Mathf.Min(1f, c.b + 0.1f));
                        if (local < 1.6f && y > 8 && y < headBase - 8 && Mathf.Abs(x - w * 0.5f) < 26f) c = outline;
                    }

                    if (d > -4f) c = outline;
                    pixels[y * w + x] = new Color(c.r, c.g, c.b, a);
                }
            }

            return ToSprite(pixels, w, h, new Vector2(0.5f, 0f), 160f);
        }

        // ------------------------------------------------------------------ Shadow

        private static Sprite MakeShadow()
        {
            const int w = 256, h = 128;
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float d = Mathf.Abs(x + 0.5f - w * 0.5f) / (w * 0.5f) + Mathf.Abs(y + 0.5f - h * 0.5f) / (h * 0.5f); // 1 at the edge
                    float a = Mathf.Clamp01((1f - d) / 0.03f);
                    if (a > 0f) pixels[y * w + x] = new Color(1f, 1f, 1f, a);
                }
            }

            return ToSprite(pixels, w, h, new Vector2(0.5f, 0.5f), 100f);
        }

        // ------------------------------------------------------------------ Shape helpers

        /// <summary>Signed distance to a convex counter-clockwise polygon (negative inside).</summary>
        private static float SdPolygon(Vector2 p, Vector2[] pts)
        {
            float dmin = float.MaxValue;
            bool inside = true;
            for (int i = 0; i < pts.Length; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % pts.Length];
                var e = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, e) / e.sqrMagnitude);
                dmin = Mathf.Min(dmin, (p - (a + t * e)).magnitude);
                if (e.x * (p.y - a.y) - e.y * (p.x - a.x) < 0f) inside = false;
            }

            return inside ? -dmin : dmin;
        }

        private static float SdRoundBox(Vector2 p, Vector2 centre, Vector2 half, float radius)
        {
            var q = new Vector2(Mathf.Abs(p.x - centre.x) - half.x + radius, Mathf.Abs(p.y - centre.y) - half.y + radius);
            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
        }

        /// <summary>The triangle pulled in by <paramref name="r"/>; draw it with <c>SdPolygon - r</c> to get rounded corners.</summary>
        private static Vector2[] InsetTriangle(Vector2 a, Vector2 b, Vector2 c, float r)
        {
            float la = (b - c).magnitude, lb = (a - c).magnitude, lc = (a - b).magnitude;
            float s = (la + lb + lc) * 0.5f;
            float area = Mathf.Abs((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y)) * 0.5f;
            float inRadius = area / s;
            var incentre = (la * a + lb * b + lc * c) / (2f * s);
            float k = (inRadius - r) / inRadius;
            return new[] { incentre + (a - incentre) * k, incentre + (b - incentre) * k, incentre + (c - incentre) * k };
        }

        private static Sprite ToSprite(Color32[] pixels, int w, int h, Vector2 pivot, float ppu)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), pivot, ppu);
        }
    }
}
