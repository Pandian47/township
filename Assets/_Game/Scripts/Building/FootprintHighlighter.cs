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

        public void Show(GridCoord origin, GridCoord size, Color color)
        {
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
