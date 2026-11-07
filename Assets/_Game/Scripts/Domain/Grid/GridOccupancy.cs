using System;
using System.Collections.Generic;

namespace TownBuilder.Domain
{
    /// <summary>
    /// Tracks which cells are buildable and which occupant (building instance id) owns each cell.
    /// Pure C#: no Unity dependency, fully unit-testable.
    /// </summary>
    public sealed class GridOccupancy
    {
        private readonly Dictionary<GridCoord, string> _occupants = new Dictionary<GridCoord, string>();
        private readonly HashSet<GridCoord> _blocked;

        public GridOccupancy(GridRect bounds, IEnumerable<GridCoord> blockedCells = null)
        {
            Bounds = bounds;
            _blocked = blockedCells != null ? new HashSet<GridCoord>(blockedCells) : new HashSet<GridCoord>();
        }

        public GridRect Bounds { get; }
        public int OccupiedCellCount => _occupants.Count;

        public bool IsBlocked(GridCoord cell) => _blocked.Contains(cell);

        public bool TryGetOccupant(GridCoord cell, out string occupantId) => _occupants.TryGetValue(cell, out occupantId);

        /// <summary>
        /// Checks whether a footprint can be placed. Cells owned by <paramref name="ignoreOccupantId"/> count as free
        /// (used when moving a building onto cells it partly covers already).
        /// </summary>
        public PlacementResult Check(GridCoord origin, GridCoord size, string ignoreOccupantId = null)
        {
            if (size.X <= 0 || size.Y <= 0) return PlacementResult.InvalidSize;
            if (!Bounds.ContainsArea(origin, size)) return PlacementResult.OutOfBounds;

            for (int dx = 0; dx < size.X; dx++)
            {
                for (int dy = 0; dy < size.Y; dy++)
                {
                    var cell = new GridCoord(origin.X + dx, origin.Y + dy);
                    if (_blocked.Contains(cell)) return PlacementResult.BlockedTerrain;
                    if (_occupants.TryGetValue(cell, out var owner) && owner != ignoreOccupantId) return PlacementResult.Overlap;
                }
            }

            return PlacementResult.Valid;
        }

        public void Occupy(string occupantId, GridCoord origin, GridCoord size)
        {
            if (string.IsNullOrEmpty(occupantId)) throw new ArgumentException("Occupant id is required.", nameof(occupantId));
            var result = Check(origin, size, occupantId);
            if (result != PlacementResult.Valid)
                throw new InvalidOperationException($"Cannot occupy {origin} size {size}: {result}");

            for (int dx = 0; dx < size.X; dx++)
                for (int dy = 0; dy < size.Y; dy++)
                    _occupants[new GridCoord(origin.X + dx, origin.Y + dy)] = occupantId;
        }

        public void Release(string occupantId, GridCoord origin, GridCoord size)
        {
            for (int dx = 0; dx < size.X; dx++)
            {
                for (int dy = 0; dy < size.Y; dy++)
                {
                    var cell = new GridCoord(origin.X + dx, origin.Y + dy);
                    if (_occupants.TryGetValue(cell, out var owner) && owner == occupantId) _occupants.Remove(cell);
                }
            }
        }
    }
}
