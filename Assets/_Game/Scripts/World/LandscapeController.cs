using System;
using System.Collections.Generic;
using TownBuilder.Domain;
using UnityEngine;
using Terrain = TownBuilder.Domain.Terrain;
using UnityEngine.Tilemaps;

namespace TownBuilder
{
    /// <summary>
    /// Landscape painting: the player picks a ground type and drags a finger (or taps) over their own land to paint it.
    /// Changes show at once; Done keeps them (saved), Cancel undoes the whole session. Water and road block building,
    /// so they never go under a building, and painting over them makes the cell buildable again.
    /// Rules live in <see cref="LandscapeEdit"/>.
    /// </summary>
    public sealed class LandscapeController : MonoBehaviour, IPointerDragHandler
    {
        [SerializeField] private IsoGrid grid;
        [SerializeField] private PointerInput pointerInput;
        [SerializeField] private PlacementSystem placement;
        [SerializeField] private SeasonController seasons;
        [SerializeField] private StreetLife streetLife;
        [SerializeField] private Tilemap groundTilemap;
        [SerializeField] private Tilemap roadTilemap;
        [SerializeField] private Tilemap waterTilemap;
        [SerializeField] private Tilemap railTilemap;
        [Tooltip("Ground tiles in Terrain order: Grass (spring), Sand, Soil, Pavement, Water, Road.")]
        [SerializeField] private TileBase[] tiles = new TileBase[6];
        [Tooltip("The railway column is never painted.")]
        [SerializeField] private int railColumn = 9;

        private Town _town;
        private LandPlots _land;
        private LandscapeMap _map;
        private LandscapeEdit _edit;
        private GridCoord? _lastCell;
        private Camera _camera;

        public event Action Started;
        public event Action Ended;
        public event Action Modified;
        public event Action<string> Notification;

        public bool IsPainting => _edit != null;
        public Terrain Selected { get; private set; } = Terrain.Grass;
        public LandscapeMap Map => _map;
        public int ChangedCount => _edit?.ChangedCount ?? 0;
        public TileBase TileFor(Terrain t) => (int)t < tiles.Length ? tiles[(int)t] : null;
        public int DragPriority => 200; // while painting, a drag paints instead of moving the camera

        /// <summary>Paints the saved cells onto the map. Call before <see cref="IsoGrid.Build"/> so blocking is right.</summary>
        public void ApplySaved(LandscapeMap map)
        {
            _map = map ?? new LandscapeMap();
            foreach (var pair in _map.Cells) SetCell(pair.Key, pair.Value, TileFor(Terrain.Grass));
        }

        public void Initialize(Town town, LandPlots land)
        {
            _town = town;
            _land = land;
            _camera = Camera.main;
            if (_map == null) _map = new LandscapeMap();
            pointerInput.RegisterDragHandler(this);
            pointerInput.Tapped += OnTapped;
        }

        private void OnDestroy()
        {
            if (pointerInput == null) return;
            pointerInput.UnregisterDragHandler(this);
            pointerInput.Tapped -= OnTapped;
        }

        // ------------------------------------------------------------------ Session

        public void Begin()
        {
            if (IsPainting || _town == null) return;
            placement.Cancel();
            placement.TapsSuspended = true;
            _edit = new LandscapeEdit(CurrentTerrain, Editable, c => _town.Occupancy.TryGetOccupant(c, out _));
            Started?.Invoke();
        }

        public void Select(Terrain terrain) => Selected = terrain;

        /// <summary>Keeps the painted cells (saved) and leaves paint mode.</summary>
        public void Done()
        {
            if (!IsPainting) return;
            int changed = _edit.ChangedCount;
            _map.Commit(_edit);
            End();
            if (changed > 0)
            {
                streetLife?.RefreshRoads();
                Notification?.Invoke(changed == 1 ? "1 tile painted." : $"{changed} tiles painted.");
                Modified?.Invoke();
            }
        }

        /// <summary>Undoes everything painted in this session.</summary>
        public void Cancel()
        {
            if (!IsPainting) return;
            foreach (var pair in _edit.Before) Apply(pair.Key, pair.Value);
            End();
        }

        private void End()
        {
            _edit = null;
            _lastCell = null;
            placement.TapsSuspended = false;
            Ended?.Invoke();
        }

        /// <summary>Paints one cell with the selected ground (tests and taps).</summary>
        public PaintResult Paint(GridCoord cell)
        {
            if (!IsPainting) return PaintResult.NoChange;
            var result = _edit.TryPaint(cell, Selected);
            if (result == PaintResult.Painted) Apply(cell, Selected);
            return result;
        }

        // ------------------------------------------------------------------ Input

        private void OnTapped(Vector2 screen)
        {
            if (!IsPainting) return;
            var result = Paint(CellAt(screen));
            if (result == PaintResult.UnderBuilding) Notification?.Invoke("Water and roads can't go under a building.");
            else if (result == PaintResult.NotYourLand) Notification?.Invoke("You can only paint your own land.");
        }

        public bool TryBeginDrag(Vector2 screenPosition)
        {
            if (!IsPainting) return false;
            _lastCell = CellAt(screenPosition);
            Paint(_lastCell.Value);
            return true;
        }

        public void Drag(Vector2 screenPosition, Vector2 screenDelta)
        {
            var cell = CellAt(screenPosition);
            foreach (var c in GridLine.Between(_lastCell ?? cell, cell)) Paint(c);
            _lastCell = cell;
        }

        public void EndDrag(Vector2 screenPosition) => _lastCell = null;

        private GridCoord CellAt(Vector2 screen) => grid.ScreenToCell(_camera != null ? _camera : Camera.main, screen);

        // ------------------------------------------------------------------ Tiles

        private bool Editable(GridCoord cell) =>
            _town.Bounds.Contains(cell) && cell.X != railColumn && !railTilemap.HasTile(V(cell)) && (_land == null || _land.IsCellUnlocked(cell));

        /// <summary>What a cell looks like now, read from the tilemaps.</summary>
        public Terrain CurrentTerrain(GridCoord cell)
        {
            var v = V(cell);
            if (waterTilemap.HasTile(v)) return Terrain.Water;
            if (roadTilemap.HasTile(v)) return Terrain.Road;
            var ground = groundTilemap.GetTile(v);
            if (ground == null) return Terrain.Water;
            if (ground == TileFor(Terrain.Sand)) return Terrain.Sand;
            if (ground == TileFor(Terrain.Soil)) return Terrain.Soil;
            if (ground == TileFor(Terrain.Pavement)) return Terrain.Pavement;
            return Terrain.Grass;
        }

        private void Apply(GridCoord cell, Terrain terrain)
        {
            var grass = seasons != null ? seasons.GrassFor(seasons.Current) ?? TileFor(Terrain.Grass) : TileFor(Terrain.Grass);
            SetCell(cell, terrain, grass);
            _town.Occupancy.SetBlocked(cell, TerrainRules.IsBlocking(terrain));
        }

        private void SetCell(GridCoord cell, Terrain terrain, TileBase grass)
        {
            var v = V(cell);
            switch (terrain)
            {
                case Terrain.Water:
                    groundTilemap.SetTile(v, null);
                    roadTilemap.SetTile(v, null);
                    waterTilemap.SetTile(v, TileFor(Terrain.Water));
                    break;
                case Terrain.Road:
                    waterTilemap.SetTile(v, null);
                    groundTilemap.SetTile(v, grass);
                    roadTilemap.SetTile(v, TileFor(Terrain.Road));
                    break;
                default:
                    waterTilemap.SetTile(v, null);
                    roadTilemap.SetTile(v, null);
                    groundTilemap.SetTile(v, terrain == Terrain.Grass ? grass : TileFor(terrain));
                    break;
            }
        }

        private static Vector3Int V(GridCoord c) => new Vector3Int(c.X, c.Y, 0);
    }
}
