using System;
using System.Collections.Generic;

namespace TownBuilder.Domain
{
    /// <summary>
    /// The player's town layout: which buildings exist and where. Source of truth for placement;
    /// Unity views subscribe to its events and never mutate it directly.
    /// </summary>
    public sealed class Town
    {
        private readonly Dictionary<string, PlacedBuilding> _buildings = new Dictionary<string, PlacedBuilding>();

        public Town(GridRect bounds, IEnumerable<GridCoord> blockedCells = null)
        {
            Occupancy = new GridOccupancy(bounds, blockedCells);
        }

        public event Action<PlacedBuilding> BuildingAdded;
        public event Action<PlacedBuilding> BuildingMoved;
        public event Action<PlacedBuilding> BuildingRemoved;

        public GridOccupancy Occupancy { get; }
        public GridRect Bounds => Occupancy.Bounds;
        public IReadOnlyCollection<PlacedBuilding> Buildings => _buildings.Values;
        public int BuildingCount => _buildings.Count;

        private Func<GridCoord, bool> _isLandUnlocked;

        /// <summary>
        /// Land expansion: cells for which this returns false can't be built on (LockedLand). Set after the saved
        /// buildings are loaded; buildings already standing are never removed.
        /// </summary>
        public void SetLandCheck(Func<GridCoord, bool> isLandUnlocked) => _isLandUnlocked = isLandUnlocked;

        public PlacementResult CanPlace(GridCoord origin, GridCoord size, string ignoreInstanceId = null) =>
            Check(origin, size, ignoreInstanceId);

        private PlacementResult Check(GridCoord origin, GridCoord size, string ignoreInstanceId = null)
        {
            var result = Occupancy.Check(origin, size, ignoreInstanceId);
            if (result != PlacementResult.Valid || _isLandUnlocked == null) return result;
            for (int dx = 0; dx < size.X; dx++)
            for (int dy = 0; dy < size.Y; dy++)
                if (!_isLandUnlocked(new GridCoord(origin.X + dx, origin.Y + dy))) return PlacementResult.LockedLand;
            return PlacementResult.Valid;
        }

        public PlacementResult TryAdd(string instanceId, string definitionId, GridCoord origin, GridCoord size, out PlacedBuilding building)
        {
            building = null;
            if (string.IsNullOrEmpty(instanceId)) throw new ArgumentException("Instance id is required.", nameof(instanceId));
            if (string.IsNullOrEmpty(definitionId)) throw new ArgumentException("Definition id is required.", nameof(definitionId));
            if (_buildings.ContainsKey(instanceId)) return PlacementResult.DuplicateId;

            var result = Check(origin, size);
            if (result != PlacementResult.Valid) return result;

            building = new PlacedBuilding(instanceId, definitionId, origin, size);
            Occupancy.Occupy(instanceId, origin, size);
            _buildings.Add(instanceId, building);
            BuildingAdded?.Invoke(building);
            return PlacementResult.Valid;
        }

        public PlacementResult TryMove(string instanceId, GridCoord newOrigin)
        {
            if (!_buildings.TryGetValue(instanceId, out var building)) return PlacementResult.NotFound;
            if (building.Origin == newOrigin) return PlacementResult.Valid;

            var result = Check(newOrigin, building.Size, instanceId);
            if (result != PlacementResult.Valid) return result;

            Occupancy.Release(instanceId, building.Origin, building.Size);
            building.Origin = newOrigin;
            Occupancy.Occupy(instanceId, newOrigin, building.Size);
            BuildingMoved?.Invoke(building);
            return PlacementResult.Valid;
        }

        public bool Remove(string instanceId)
        {
            if (instanceId == null || !_buildings.TryGetValue(instanceId, out var building)) return false;
            Occupancy.Release(instanceId, building.Origin, building.Size);
            _buildings.Remove(instanceId);
            BuildingRemoved?.Invoke(building);
            return true;
        }

        public int CountOf(string definitionId)
        {
            int count = 0;
            foreach (var b in _buildings.Values)
                if (b.DefinitionId == definitionId) count++;
            return count;
        }

        public bool TryGet(string instanceId, out PlacedBuilding building)
        {
            building = null;
            return instanceId != null && _buildings.TryGetValue(instanceId, out building);
        }

        public bool TryGetAt(GridCoord cell, out PlacedBuilding building)
        {
            building = null;
            return Occupancy.TryGetOccupant(cell, out var id) && _buildings.TryGetValue(id, out building);
        }
    }
}
