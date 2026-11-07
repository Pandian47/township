using UnityEngine;

namespace TownBuilder
{
    public static class IsoMath
    {
        /// <summary>
        /// Fills <paramref name="hexagon"/> (length 6) with the silhouette of a footprint extruded upward:
        /// the footprint diamond (left, bottom, right) plus the same points raised to <paramref name="topY"/>.
        /// This is the outline of any isometric building sprite and is used for tap hit-testing.
        /// </summary>
        public static void BuildingSilhouette(Vector2 anchor, Vector2 axisX, Vector2 axisY, int sizeX, int sizeY, float topY, Vector2[] hexagon)
        {
            var bottom = anchor;
            var right = anchor + axisX * sizeX;
            var left = anchor + axisY * sizeY;
            var top = right + axisY * sizeY;
            float raise = Mathf.Max(0f, topY - top.y);
            var up = new Vector2(0f, raise);

            hexagon[0] = left;
            hexagon[1] = bottom;
            hexagon[2] = right;
            hexagon[3] = right + up;
            hexagon[4] = top + up;
            hexagon[5] = left + up;
        }

        /// <summary>Point-in-polygon for convex polygons given in either winding order.</summary>
        public static bool ConvexContains(Vector2[] polygon, Vector2 point)
        {
            int sign = 0;
            for (int i = 0; i < polygon.Length; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Length];
                float cross = (b.x - a.x) * (point.y - a.y) - (b.y - a.y) * (point.x - a.x);
                if (Mathf.Abs(cross) < 1e-6f) continue;
                int s = cross > 0 ? 1 : -1;
                if (sign == 0) sign = s;
                else if (s != sign) return false;
            }

            return true;
        }
    }
}
