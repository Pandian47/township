using System.Collections.Generic;
using TownBuilder.Domain;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TownBuilder
{
    /// <summary>
    /// Bridge between Unity's isometric <see cref="Grid"/>/<see cref="Tilemap"/> and the pure grid model.
    /// The map is authored by painting the tilemaps; buildable area = cells with a ground tile and no blocking tile.
    ///
    /// Conventions (Grid cell layout = Isometric, cell size = 1 x 0.5):
    ///  * CellToWorld(cell) returns the BOTTOM corner of that cell's diamond.
    ///  * A footprint's anchor is the bottom corner of its origin (lowest x, lowest y) cell, which is also
    ///    where building sprite pivots go.
    /// </summary>
    public sealed class IsoGrid : MonoBehaviour
    {
        [SerializeField] private Grid grid;
        [SerializeField] private Tilemap groundTilemap;
        [Tooltip("Cells painted on any of these tilemaps can't be built on (water, roads...).")]
        [SerializeField] private Tilemap[] blockingTilemaps = new Tilemap[0];

        private readonly HashSet<GridCoord> _blockedCells = new HashSet<GridCoord>();

        public Grid UnityGrid => grid;
        public GridRect Bounds { get; private set; }
        public IReadOnlyCollection<GridCoord> BlockedCells => _blockedCells;

        /// <summary>World offset of one step along the grid X axis (up and to the right on screen).</summary>
        public Vector2 AxisX => (Vector2)(grid.CellToWorld(new Vector3Int(1, 0, 0)) - grid.CellToWorld(Vector3Int.zero));

        /// <summary>World offset of one step along the grid Y axis (up and to the left on screen).</summary>
        public Vector2 AxisY => (Vector2)(grid.CellToWorld(new Vector3Int(0, 1, 0)) - grid.CellToWorld(Vector3Int.zero));

        /// <summary>Reads the painted tilemaps and computes bounds + blocked cells. Call once the scene is loaded.</summary>
        public void Build()
        {
            groundTilemap.CompressBounds();
            var b = groundTilemap.cellBounds;
            Bounds = new GridRect(b.xMin, b.yMin, b.size.x, b.size.y);

            _blockedCells.Clear();
            for (int x = b.xMin; x < b.xMax; x++)
            {
                for (int y = b.yMin; y < b.yMax; y++)
                {
                    var cell = new Vector3Int(x, y, 0);
                    bool blocked = !groundTilemap.HasTile(cell);
                    if (!blocked)
                    {
                        foreach (var map in blockingTilemaps)
                        {
                            if (map != null && map.HasTile(cell)) { blocked = true; break; }
                        }
                    }

                    if (blocked) _blockedCells.Add(new GridCoord(x, y));
                }
            }
        }

        public GridCoord WorldToCell(Vector2 world)
        {
            var c = grid.WorldToCell(new Vector3(world.x, world.y, 0f));
            return new GridCoord(c.x, c.y);
        }

        public Vector2 ScreenToWorld(Camera cam, Vector2 screen)
        {
            var w = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            return new Vector2(w.x, w.y);
        }

        public GridCoord ScreenToCell(Camera cam, Vector2 screen) => WorldToCell(ScreenToWorld(cam, screen));

        public Vector3 CellCenterWorld(GridCoord cell) => grid.GetCellCenterWorld(new Vector3Int(cell.X, cell.Y, 0));

        /// <summary>Bottom corner of the footprint whose lowest cell is <paramref name="origin"/>.</summary>
        public Vector3 FootprintAnchorWorld(GridCoord origin) => grid.CellToWorld(new Vector3Int(origin.X, origin.Y, 0));

        /// <summary>World position of the visual centre of a footprint (used to centre the camera or UI).</summary>
        public Vector3 FootprintCenterWorld(GridCoord origin, GridCoord size)
        {
            var anchor = (Vector2)FootprintAnchorWorld(origin);
            var c = anchor + AxisX * (size.X * 0.5f) + AxisY * (size.Y * 0.5f);
            return new Vector3(c.x, c.y, 0f);
        }

        /// <summary>Axis-aligned world rectangle enclosing the whole map diamond.</summary>
        public Rect WorldBounds()
        {
            if (Bounds.IsEmpty) return new Rect();
            var corners = new[]
            {
                grid.CellToWorld(new Vector3Int(Bounds.MinX, Bounds.MinY, 0)),
                grid.CellToWorld(new Vector3Int(Bounds.MaxX, Bounds.MinY, 0)),
                grid.CellToWorld(new Vector3Int(Bounds.MinX, Bounds.MaxY, 0)),
                grid.CellToWorld(new Vector3Int(Bounds.MaxX, Bounds.MaxY, 0)),
            };

            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var c in corners)
            {
                minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x);
                minY = Mathf.Min(minY, c.y); maxY = Mathf.Max(maxY, c.y);
            }

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }
    }
}
