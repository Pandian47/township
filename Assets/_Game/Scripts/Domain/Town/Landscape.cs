using System;
using System.Collections.Generic;

namespace TownBuilder.Domain
{
    /// <summary>Ground a player can paint. Saved by name: never rename a value without a save migration.</summary>
    public enum Terrain
    {
        Grass,
        Sand,
        Soil,
        Pavement,
        Water,
        Road,
    }

    public enum PaintResult
    {
        Painted,
        NoChange,
        NotYourLand,
        UnderBuilding,
    }

    public static class TerrainRules
    {
        /// <summary>Water and roads can't be built on.</summary>
        public static bool IsBlocking(Terrain t) => t == Terrain.Water || t == Terrain.Road;
    }

    /// <summary>
    /// One painting session: cells changed so far, with what they were before (to undo on Cancel) and what they are now.
    /// Rules: only editable cells (own land, not the railway); water and road never under a building.
    /// </summary>
    public sealed class LandscapeEdit
    {
        private readonly Func<GridCoord, Terrain> _current;
        private readonly Func<GridCoord, bool> _editable;
        private readonly Func<GridCoord, bool> _occupied;
        private readonly Dictionary<GridCoord, Terrain> _before = new Dictionary<GridCoord, Terrain>();
        private readonly Dictionary<GridCoord, Terrain> _after = new Dictionary<GridCoord, Terrain>();

        public LandscapeEdit(Func<GridCoord, Terrain> current, Func<GridCoord, bool> editable, Func<GridCoord, bool> occupied)
        {
            _current = current;
            _editable = editable;
            _occupied = occupied;
        }

        public IReadOnlyDictionary<GridCoord, Terrain> Before => _before;
        public IReadOnlyDictionary<GridCoord, Terrain> After => _after;
        public int ChangedCount => _after.Count;

        public PaintResult TryPaint(GridCoord cell, Terrain terrain)
        {
            if (!_editable(cell)) return PaintResult.NotYourLand;
            if (TerrainRules.IsBlocking(terrain) && _occupied(cell)) return PaintResult.UnderBuilding;
            var now = _current(cell);
            if (now == terrain) return PaintResult.NoChange;
            if (!_before.ContainsKey(cell)) _before[cell] = now;
            if (_before[cell] == terrain) _after.Remove(cell); // painted back to how it was
            else _after[cell] = terrain;
            return PaintResult.Painted;
        }
    }

    /// <summary>The player's painted cells (on top of the town's starting map). Saved.</summary>
    public sealed class LandscapeMap
    {
        private readonly Dictionary<GridCoord, Terrain> _cells = new Dictionary<GridCoord, Terrain>();

        public IReadOnlyDictionary<GridCoord, Terrain> Cells => _cells;

        public void Set(GridCoord cell, Terrain terrain) => _cells[cell] = terrain;

        public void Commit(LandscapeEdit edit)
        {
            foreach (var pair in edit.After) _cells[pair.Key] = pair.Value;
        }
    }

    /// <summary>Lines of grid cells between two cells (so a quick finger swipe leaves no gaps).</summary>
    public static class GridLine
    {
        public static IEnumerable<GridCoord> Between(GridCoord a, GridCoord b)
        {
            int steps = Math.Max(Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));
            if (steps == 0)
            {
                yield return a;
                yield break;
            }

            for (int i = 0; i <= steps; i++)
            {
                double t = i / (double)steps;
                yield return new GridCoord((int)Math.Round(a.X + (b.X - a.X) * t), (int)Math.Round(a.Y + (b.Y - a.Y) * t));
            }
        }
    }
}
