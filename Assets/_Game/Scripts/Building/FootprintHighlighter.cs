using System.Collections.Generic;
using TownBuilder.Domain;
using UnityEngine;

namespace TownBuilder
{
    /// <summary>Draws a tinted diamond on each cell of a footprint (placement preview, selection).</summary>
    public sealed class FootprintHighlighter : MonoBehaviour
    {
        [SerializeField] private IsoGrid grid;
        [SerializeField] private Sprite cellSprite;

        private readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>();

        private SpriteRenderer _arrowLeft;
        private SpriteRenderer _arrowRight;
        private SpriteRenderer _shadow;

        /// <summary>
        /// Edit mode (v0.42.0): one soft dark diamond on the ground under the picked-up building, and a corner arrow outside
        /// each lower edge of its footprint. A blocked spot turns the shadow and the arrows red.
        /// </summary>
        public void ShowEdit(GridCoord origin, GridCoord size, bool valid, Color shadowColor, Color blockedColor)
        {
            Hide(); // no cell tiles in edit mode

            if (_shadow == null) _shadow = CreateCue("Shadow", CueSprites.Shadow, false, 1);
            if (_arrowLeft == null) _arrowLeft = CreateCue("ArrowLeft", CueSprites.CornerArrow, false, 5);
            if (_arrowRight == null) _arrowRight = CreateCue("ArrowRight", CueSprites.CornerArrow, true, 5);

            var anchor = grid.FootprintAnchorWorld(origin);
            var c1 = anchor + (Vector3)(grid.AxisX * size.X);
            var c2 = anchor + (Vector3)(grid.AxisY * size.Y);
            var top = c1 + (Vector3)(grid.AxisY * size.Y);
            var left = c1.x < c2.x ? c1 : c2;
            var right = c1.x < c2.x ? c2 : c1;

            float width = Mathf.Abs(c1.x - c2.x);
            float height = top.y - anchor.y;
            var centre = (anchor + top) * 0.5f;
            _shadow.transform.position = new Vector3(centre.x, centre.y, 0f);
            _shadow.transform.localScale = new Vector3(width * 1.02f / CueSprites.ShadowSize.x, height * 1.02f / CueSprites.ShadowSize.y, 1f);
            _shadow.color = valid ? shadowColor : blockedColor;
            _shadow.enabled = true;

            // The arrows hang just under the lower-left and lower-right edges (the shape follows the edge's slope).
            var arrowColor = valid ? new Color(0.72f, 1f, 0.12f, 1f) : new Color(1f, 0.38f, 0.32f, 1f);
            float arrowScale = Mathf.Max(0.3f, width * 0.11f) / CueSprites.CornerArrowWidth;
            Place(_arrowLeft, Vector3.Lerp(left, anchor, 0.2f) + new Vector3(0f, -0.06f, 0f), arrowScale, arrowColor);
            Place(_arrowRight, Vector3.Lerp(right, anchor, 0.2f) + new Vector3(0f, -0.06f, 0f), arrowScale, arrowColor);
        }

        private static void Place(SpriteRenderer r, Vector3 position, float scale, Color color)
        {
            r.transform.position = new Vector3(position.x, position.y, 0f);
            r.transform.localScale = new Vector3(scale, scale, 1f);
            r.color = color;
            r.enabled = true;
        }

        private SpriteRenderer CreateCue(string name, Sprite sprite, bool flipX, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.flipX = flipX;
            r.sortingLayerName = SortingLayers.GroundOverlay;
            r.sortingOrder = order;
            r.enabled = false;
            return r;
        }

        private void HideArrows()
        {
            if (_arrowLeft != null) _arrowLeft.enabled = false;
            if (_arrowRight != null) _arrowRight.enabled = false;
            if (_shadow != null) _shadow.enabled = false;
        }

        public void Show(GridCoord origin, GridCoord size, Color color)
        {
            HideArrows();
            int needed = size.X * size.Y;
            while (_pool.Count < needed) _pool.Add(CreateCell(_pool.Count));

            int i = 0;
            for (int dx = 0; dx < size.X; dx++)
            {
                for (int dy = 0; dy < size.Y; dy++)
                {
                    var r = _pool[i++];
                    r.transform.position = grid.CellCenterWorld(new GridCoord(origin.X + dx, origin.Y + dy));
                    r.color = color;
                    r.enabled = true;
                }
            }

            for (; i < _pool.Count; i++) _pool[i].enabled = false;
        }

        public void Hide()
        {
            HideArrows();
            foreach (var r in _pool) r.enabled = false;
        }

        private SpriteRenderer CreateCell(int index)
        {
            var go = new GameObject($"Cell_{index}");
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = cellSprite;
            r.sortingLayerName = SortingLayers.GroundOverlay;
            r.enabled = false;
            return r;
        }
    }
}
