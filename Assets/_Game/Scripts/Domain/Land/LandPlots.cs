using System;
using System.Collections.Generic;

namespace TownBuilder.Domain
{
    /// <summary>A square piece of land (plot coordinates, not cells).</summary>
    public readonly struct PlotCoord : IEquatable<PlotCoord>
    {
        public PlotCoord(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }

        /// <summary>Permanent id used in saves, e.g. "3_2".</summary>
        public string Id => $"{X}_{Y}";

        public static bool TryParse(string id, out PlotCoord plot)
        {
            plot = default;
            if (string.IsNullOrEmpty(id)) return false;
            var parts = id.Split('_');
            if (parts.Length != 2 || !int.TryParse(parts[0], out int x) || !int.TryParse(parts[1], out int y)) return false;
            plot = new PlotCoord(x, y);
            return true;
        }

        public bool Equals(PlotCoord other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is PlotCoord other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ Y;
        public override string ToString() => Id;
    }

    /// <summary>
    /// The town's land, split into square plots. A new town owns the plots of the start area; the others are bought one
    /// at a time, each next to land already owned. Buildings may only stand on owned land.
    /// The ring (distance in plots from the start area) decides a plot's price tier.
    /// </summary>
    public sealed class LandPlots
    {
        private readonly HashSet<PlotCoord> _owned = new HashSet<PlotCoord>();

        /// <param name="bounds">Cell bounds of the map.</param>
        /// <param name="plotSize">Plot edge length in cells.</param>
        /// <param name="startMin">First plot of the start area (inclusive).</param>
        /// <param name="startMax">Last plot of the start area (inclusive).</param>
        public LandPlots(GridRect bounds, int plotSize, PlotCoord startMin, PlotCoord startMax)
        {
            Bounds = bounds;
            PlotSize = Math.Max(1, plotSize);
            Columns = (bounds.Width + PlotSize - 1) / PlotSize;
            Rows = (bounds.Height + PlotSize - 1) / PlotSize;
            StartMin = startMin;
            StartMax = startMax;
        }

        public event Action<PlotCoord> PlotBought;
        /// <summary>Ownership changed (bought, restored).</summary>
        public event Action Changed;

        public GridRect Bounds { get; }
        public int PlotSize { get; }
        public int Columns { get; }
        public int Rows { get; }
        public PlotCoord StartMin { get; }
        public PlotCoord StartMax { get; }
        public IReadOnlyCollection<PlotCoord> Owned => _owned;

        public bool IsValid(PlotCoord p) => p.X >= 0 && p.Y >= 0 && p.X < Columns && p.Y < Rows;

        public bool IsOwned(PlotCoord p) => _owned.Contains(p);

        public PlotCoord PlotOf(GridCoord cell) =>
            new PlotCoord(FloorDiv(cell.X - Bounds.MinX, PlotSize), FloorDiv(cell.Y - Bounds.MinY, PlotSize));

        public bool IsCellUnlocked(GridCoord cell) => IsOwned(PlotOf(cell));

        /// <summary>First cell (lowest x/y) of a plot.</summary>
        public GridCoord PlotOrigin(PlotCoord p) => new GridCoord(Bounds.MinX + p.X * PlotSize, Bounds.MinY + p.Y * PlotSize);

        /// <summary>Plot edge sizes in cells (the last row/column may be cut by the map edge).</summary>
        public GridCoord PlotCells(PlotCoord p)
        {
            var o = PlotOrigin(p);
            return new GridCoord(Math.Min(PlotSize, Bounds.MaxX - o.X), Math.Min(PlotSize, Bounds.MaxY - o.Y));
        }

        /// <summary>0 inside the start area, 1 for plots touching it, 2 for the next ring...</summary>
        public int Ring(PlotCoord p)
        {
            int dx = p.X < StartMin.X ? StartMin.X - p.X : p.X > StartMax.X ? p.X - StartMax.X : 0;
            int dy = p.Y < StartMin.Y ? StartMin.Y - p.Y : p.Y > StartMax.Y ? p.Y - StartMax.Y : 0;
            return Math.Max(dx, dy);
        }

        /// <summary>Not owned yet and next to (sharing an edge with) an owned plot.</summary>
        public bool IsForSale(PlotCoord p)
        {
            if (!IsValid(p) || IsOwned(p)) return false;
            return IsOwned(new PlotCoord(p.X - 1, p.Y)) || IsOwned(new PlotCoord(p.X + 1, p.Y)) ||
                   IsOwned(new PlotCoord(p.X, p.Y - 1)) || IsOwned(new PlotCoord(p.X, p.Y + 1));
        }

        public IEnumerable<PlotCoord> PlotsForSale()
        {
            for (int y = 0; y < Rows; y++)
            for (int x = 0; x < Columns; x++)
            {
                var p = new PlotCoord(x, y);
                if (IsForSale(p)) yield return p;
            }
        }

        /// <summary>Marks a plot as owned. Costs are checked and paid by the caller.</summary>
        public bool Buy(PlotCoord p)
        {
            if (!IsForSale(p)) return false;
            _owned.Add(p);
            PlotBought?.Invoke(p);
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Land for a town without land data (new game or a save from before land existed): the start area plus every
        /// plot that already has a building on it, so nobody loses a building.
        /// </summary>
        public void InitializeDefault(IEnumerable<GridCoord> occupiedCells)
        {
            _owned.Clear();
            for (int y = StartMin.Y; y <= StartMax.Y; y++)
            for (int x = StartMin.X; x <= StartMax.X; x++)
            {
                var p = new PlotCoord(x, y);
                if (IsValid(p)) _owned.Add(p);
            }

            if (occupiedCells != null)
                foreach (var cell in occupiedCells)
                {
                    var p = PlotOf(cell);
                    if (IsValid(p)) _owned.Add(p);
                }

            Changed?.Invoke();
        }

        public void Restore(IEnumerable<PlotCoord> owned)
        {
            _owned.Clear();
            foreach (var p in owned)
                if (IsValid(p)) _owned.Add(p);
            Changed?.Invoke();
        }

        private static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);
    }
}
