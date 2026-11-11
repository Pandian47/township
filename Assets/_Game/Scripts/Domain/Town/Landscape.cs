using System;
using System.Collections.Generic;

namespace TownBuilder.Domain
{
    /// <summary>
    /// Ground a player can paint. Saved by name: never rename a value without a save migration. New values go at the end.
    /// Road = "Stone road", Pavement = "Patio tiles" in the game (names kept for old saves).
    /// </summary>
    public enum Terrain
    {
        Grass,
        Sand,
        Soil,
        Pavement,
        Water,
        Road,
        // v0.36.0: more styles per tool.
        DirtPath,
        BrickLane,
        Boardwalk,
        CheckerTiles,
        StoneSlabs,
        Meadow,
    }

    /// <summary>The three free landscape tools (shop cards). Ground is 0 so older tool cards stay Ground.</summary>
    public enum LandscapeTool
    {
        Ground,
        Roads,
        Paving,
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
        public static bool IsRoad(Terrain t) => t == Terrain.Road || t == Terrain.DirtPath || t == Terrain.BrickLane || t == Terrain.Boardwalk;

        public static bool IsPaving(Terrain t) => t == Terrain.Pavement || t == Terrain.CheckerTiles || t == Terrain.StoneSlabs;

        /// <summary>Water and roads can't be built on.</summary>
        public static bool IsBlocking(Terrain t) => t == Terrain.Water || IsRoad(t);
    }

    /// <summary>Which styles each tool offers, in bar order, and their names in the game.</summary>
    public static class LandscapePalette
    {
        private static readonly Terrain[] Roads = { Terrain.Road, Terrain.DirtPath, Terrain.BrickLane, Terrain.Boardwalk };
        private static readonly Terrain[] Paving = { Terrain.Pavement, Terrain.CheckerTiles, Terrain.StoneSlabs };
        private static readonly Terrain[] Ground = { Terrain.Grass, Terrain.Meadow, Terrain.Sand, Terrain.Soil, Terrain.Water };

        public static IReadOnlyList<Terrain> For(LandscapeTool tool) =>
            tool == LandscapeTool.Roads ? Roads : tool == LandscapeTool.Paving ? Paving : (IReadOnlyList<Terrain>)Ground;

        public static string Name(Terrain t)
        {
            switch (t)
            {
                case Terrain.Road: return "Stone road";
                case Terrain.DirtPath: return "Dirt path";
                case Terrain.BrickLane: return "Brick lane";
                case Terrain.Boardwalk: return "Boardwalk";
                case Terrain.Pavement: return "Patio tiles";
                case Terrain.CheckerTiles: return "Checker tiles";
                case Terrain.StoneSlabs: return "Stone slabs";
                case Terrain.Meadow: return "Meadow";
                default: return t.ToString();
            }
        }

        public static string Title(LandscapeTool tool) =>
            tool == LandscapeTool.Roads ? "Roads & paths" : tool == LandscapeTool.Paving ? "Paving" : "Landscape";
    }

    /// <summary>
    /// A road route between two cells along the grid lines: one straight leg, or two legs with one corner.
    /// <paramref name="xFirst"/> picks which leg comes first (where the corner is).
    /// </summary>
    public static class RoadRoute
    {
        public static List<GridCoord> Path(GridCoord start, GridCoord end, bool xFirst)
        {
            var cells = new List<GridCoord> { start };
            var at = start;
            void Walk(bool alongX)
            {
                int target = alongX ? end.X : end.Y;
                while ((alongX ? at.X : at.Y) != target)
                {
                    int step = Math.Sign(target - (alongX ? at.X : at.Y));
                    at = alongX ? new GridCoord(at.X + step, at.Y) : new GridCoord(at.X, at.Y + step);
                    cells.Add(at);
                }
            }

            Walk(xFirst);
            Walk(!xFirst);
            return cells;
        }

        /// <summary>The longer leg first: dragging mostly along X gives a corner near the flag.</summary>
        public static bool PreferXFirst(GridCoord start, GridCoord end) => Math.Abs(end.X - start.X) >= Math.Abs(end.Y - start.Y);
    }

    /// <summary>
    /// Which sides of a road cell touch another road. Sides without a road get a kerb, so connected roads join up.
    /// Bit order matches the drawn tile variants: +X (screen up-right), +Y (up-left), -X (down-left), -Y (down-right).
    /// </summary>
    public static class RoadJoins
    {
        public const int PlusX = 1, PlusY = 2, MinusX = 4, MinusY = 8;

        public static int Mask(GridCoord cell, Func<GridCoord, bool> isRoad)
        {
            int mask = 0;
            if (isRoad(new GridCoord(cell.X + 1, cell.Y))) mask |= PlusX;
            if (isRoad(new GridCoord(cell.X, cell.Y + 1))) mask |= PlusY;
            if (isRoad(new GridCoord(cell.X - 1, cell.Y))) mask |= MinusX;
            if (isRoad(new GridCoord(cell.X, cell.Y - 1))) mask |= MinusY;
            return mask;
        }
    }

    /// <summary>
    /// One painting session: cells changed so far, with what they were before (to undo everything on Cancel) and what they
    /// are now, plus strokes (one finger drag, one tap or one road route) for step-by-step Undo.
    /// Rules: only editable cells (own land, not the railway); water and roads never under a building.
    /// </summary>
    public sealed class LandscapeEdit
    {
        private readonly Func<GridCoord, Terrain> _current;
        private readonly Func<GridCoord, bool> _editable;
        private readonly Func<GridCoord, bool> _occupied;
        private readonly Dictionary<GridCoord, Terrain> _before = new Dictionary<GridCoord, Terrain>();
        private readonly Dictionary<GridCoord, Terrain> _after = new Dictionary<GridCoord, Terrain>();
        private readonly List<List<KeyValuePair<GridCoord, Terrain>>> _strokes = new List<List<KeyValuePair<GridCoord, Terrain>>>();
        private List<KeyValuePair<GridCoord, Terrain>> _open;

        public LandscapeEdit(Func<GridCoord, Terrain> current, Func<GridCoord, bool> editable, Func<GridCoord, bool> occupied)
        {
            _current = current;
            _editable = editable;
            _occupied = occupied;
        }

        public IReadOnlyDictionary<GridCoord, Terrain> Before => _before;
        public IReadOnlyDictionary<GridCoord, Terrain> After => _after;
        public int ChangedCount => _after.Count;
        public bool CanUndo => _strokes.Count > 0;

        /// <summary>Would painting this cell work? Changes nothing.</summary>
        public PaintResult Check(GridCoord cell, Terrain terrain)
        {
            if (!_editable(cell)) return PaintResult.NotYourLand;
            if (TerrainRules.IsBlocking(terrain) && _occupied(cell)) return PaintResult.UnderBuilding;
            return _current(cell) == terrain ? PaintResult.NoChange : PaintResult.Painted;
        }

        /// <summary>Starts a stroke: everything painted until <see cref="EndStroke"/> is undone together.</summary>
        public void BeginStroke()
        {
            EndStroke();
            _open = new List<KeyValuePair<GridCoord, Terrain>>();
        }

        public void EndStroke()
        {
            if (_open != null && _open.Count > 0) _strokes.Add(_open);
            _open = null;
        }

        public PaintResult TryPaint(GridCoord cell, Terrain terrain)
        {
            var check = Check(cell, terrain);
            if (check != PaintResult.Painted) return check;
            var now = _current(cell);
            bool single = _open == null;
            if (single) _open = new List<KeyValuePair<GridCoord, Terrain>>();
            if (!_open.Exists(p => p.Key.Equals(cell))) _open.Add(new KeyValuePair<GridCoord, Terrain>(cell, now));
            if (single) EndStroke();

            if (!_before.ContainsKey(cell)) _before[cell] = now;
            if (_before[cell] == terrain) _after.Remove(cell); // painted back to how it was
            else _after[cell] = terrain;
            return PaintResult.Painted;
        }

        /// <summary>Takes back the last stroke. Returns the cells and what to show there again.</summary>
        public IReadOnlyList<KeyValuePair<GridCoord, Terrain>> UndoLastStroke()
        {
            EndStroke();
            if (_strokes.Count == 0) return Array.Empty<KeyValuePair<GridCoord, Terrain>>();
            var stroke = _strokes[_strokes.Count - 1];
            _strokes.RemoveAt(_strokes.Count - 1);
            var restored = new List<KeyValuePair<GridCoord, Terrain>>();
            for (int i = stroke.Count - 1; i >= 0; i--)
            {
                var (cell, previous) = (stroke[i].Key, stroke[i].Value);
                if (_before.TryGetValue(cell, out var original) && original == previous) _after.Remove(cell);
                else _after[cell] = previous;
                restored.Add(new KeyValuePair<GridCoord, Terrain>(cell, previous));
            }

            return restored;
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
