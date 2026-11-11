using System;
using System.Collections.Generic;
using TownBuilder.Domain;
using UnityEngine;
using Terrain = TownBuilder.Domain.Terrain;
using UnityEngine.Tilemaps;

namespace TownBuilder
{
    /// <summary>
    /// The three landscape tools (v0.36.0): Roads &amp; paths, Paving and Landscape (ground).
    /// * Paving and Landscape: pick a style and drag a finger (or tap) over your own land to paint it.
    /// * Roads: tap where the road starts and drag the flag to where it ends; the route follows the grid with at most one
    ///   corner (green = can be built, red = crosses a building or land you don't own). The green button lays it.
    /// * Eraser (every tool): turns cells back into grass. Undo takes back the last stroke or road.
    /// Changes show at once; Done keeps them (saved), Cancel undoes the whole session. Water and roads block building,
    /// so they never go under a building, and painting over them makes the cell buildable again.
    /// Rules live in <see cref="LandscapeEdit"/>, <see cref="RoadRoute"/> and <see cref="RoadJoins"/>.
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
        [Tooltip("Tiles in Terrain order (Grass, Sand, Soil, Pavement, Water, Road, DirtPath, BrickLane, Boardwalk, " +
                 "CheckerTiles, StoneSlabs, Meadow). Roads are JoinedRoadTiles.")]
        [SerializeField] private TileBase[] tiles = new TileBase[12];
        [Tooltip("The railway column is never painted.")]
        [SerializeField] private int railColumn = 9;

        [Header("Road route preview")]
        [SerializeField] private Sprite cellSprite;
        [SerializeField] private Sprite flagSprite;

        private static readonly Color RouteOk = new Color(0.25f, 1f, 0.35f, 0.85f);
        private static readonly Color RouteBad = new Color(1f, 0.25f, 0.2f, 0.85f);

        private Town _town;
        private LandPlots _land;
        private LandscapeMap _map;
        private LandscapeEdit _edit;
        private GridCoord? _lastCell;
        private Camera _camera;

        private GridCoord? _routeStart;
        private GridCoord _routeEnd;
        private bool _routeXFirst;
        private bool _cornerFlipped;
        private List<GridCoord> _route = new List<GridCoord>();
        private readonly List<SpriteRenderer> _markers = new List<SpriteRenderer>();
        private SpriteRenderer _flag;

        public event Action Started;
        public event Action Ended;
        public event Action Modified;
        /// <summary>Selection, route or undo state changed (the paint bar refreshes).</summary>
        public event Action StateChanged;
        public event Action<string> Notification;

        public bool IsPainting => _edit != null;
        public LandscapeTool Tool { get; private set; } = LandscapeTool.Ground;
        public Terrain Selected { get; private set; } = Terrain.Grass;
        public bool Erasing { get; private set; }
        public LandscapeMap Map => _map;
        public int ChangedCount => _edit?.ChangedCount ?? 0;
        public bool CanUndo => _edit != null && _edit.CanUndo;
        public bool HasRoute => _routeStart.HasValue;
        public bool RouteValid { get; private set; }
        public IReadOnlyList<GridCoord> Route => _route;
        public bool DrawsRoutes => Tool == LandscapeTool.Roads && !Erasing;
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

        public void Begin(LandscapeTool tool = LandscapeTool.Ground)
        {
            if (IsPainting || _town == null) return;
            placement.Cancel();
            placement.TapsSuspended = true;
            Tool = tool;
            Selected = LandscapePalette.For(tool)[0];
            Erasing = false;
            _edit = new LandscapeEdit(CurrentTerrain, Editable, c => _town.Occupancy.TryGetOccupant(c, out _));
            Started?.Invoke();
            StateChanged?.Invoke();
        }

        public void Select(Terrain terrain)
        {
            Selected = terrain;
            Erasing = false;
            if (HasRoute) UpdateRoute(); // same route, new style
            StateChanged?.Invoke();
        }

        public void SelectEraser()
        {
            Erasing = true;
            DiscardRoute();
            StateChanged?.Invoke();
        }

        /// <summary>The green button: lays the pending road, or (with no road pending) keeps everything and leaves.</summary>
        public void Confirm()
        {
            if (HasRoute) LayRoute();
            else Done();
        }

        /// <summary>The red button: removes the pending road, or (with none pending) undoes the whole session and leaves.</summary>
        public void Back()
        {
            if (HasRoute) DiscardRoute();
            else Cancel();
        }

        /// <summary>Keeps the painted cells (saved) and leaves paint mode.</summary>
        public void Done()
        {
            if (!IsPainting) return;
            DiscardRoute();
            int changed = _edit.ChangedCount;
            _map.Commit(_edit);
            End();
            if (changed > 0)
            {
                streetLife?.RefreshRoads();
                Notification?.Invoke(changed == 1 ? "1 tile changed." : $"{changed} tiles changed.");
                Modified?.Invoke();
            }
        }

        /// <summary>Undoes everything painted in this session.</summary>
        public void Cancel()
        {
            if (!IsPainting) return;
            DiscardRoute();
            foreach (var pair in _edit.Before) Apply(pair.Key, pair.Value);
            End();
        }

        /// <summary>Takes back the last stroke (one drag, one tap or one road).</summary>
        public void Undo()
        {
            if (!IsPainting) return;
            DiscardRoute();
            foreach (var pair in _edit.UndoLastStroke()) Apply(pair.Key, pair.Value);
            StateChanged?.Invoke();
        }

        private void End()
        {
            _edit = null;
            _lastCell = null;
            placement.TapsSuspended = false;
            Ended?.Invoke();
        }

        /// <summary>Paints one cell with the selected ground (or grass when erasing) as its own stroke (tests and taps).</summary>
        public PaintResult Paint(GridCoord cell)
        {
            if (!IsPainting) return PaintResult.NoChange;
            var terrain = Erasing ? Terrain.Grass : Selected;
            var result = _edit.TryPaint(cell, terrain);
            if (result == PaintResult.Painted) Apply(cell, terrain);
            return result;
        }

        // ------------------------------------------------------------------ Road routes

        /// <summary>Shows a road route from <paramref name="start"/> to <paramref name="end"/> (the flag).</summary>
        public void SetRoute(GridCoord start, GridCoord end)
        {
            if (!IsPainting) return;
            bool newStart = !_routeStart.HasValue || !_routeStart.Value.Equals(start);
            _routeStart = start;
            _routeEnd = end;
            if (newStart) _cornerFlipped = false;
            UpdateRoute();
            StateChanged?.Invoke();
        }

        /// <summary>Puts the corner on the other side (the other leg first).</summary>
        public void FlipCorner()
        {
            if (!HasRoute) return;
            _cornerFlipped = !_cornerFlipped;
            UpdateRoute();
            StateChanged?.Invoke();
        }

        /// <summary>Builds the pending route as one stroke. False if it crosses a building or land you don't own.</summary>
        public bool LayRoute()
        {
            if (!IsPainting || !HasRoute) return false;
            if (!RouteValid)
            {
                Notification?.Invoke("The road can't cross buildings or land you don't own. Move the flag.");
                return false;
            }

            _edit.BeginStroke();
            foreach (var cell in _route)
                if (_edit.TryPaint(cell, Selected) == PaintResult.Painted) Apply(cell, Selected);
            _edit.EndStroke();
            DiscardRoute();
            StateChanged?.Invoke();
            return true;
        }

        public void DiscardRoute()
        {
            bool had = HasRoute;
            _routeStart = null;
            _route.Clear();
            RouteValid = false;
            ShowRoutePreview(_route, false);
            if (had) StateChanged?.Invoke();
        }

        private void UpdateRoute()
        {
            var start = _routeStart.Value;
            _routeXFirst = RoadRoute.PreferXFirst(start, _routeEnd) != _cornerFlipped;
            _route = RoadRoute.Path(start, _routeEnd, _routeXFirst);
            RouteValid = true;
            foreach (var cell in _route)
            {
                var check = _edit.Check(cell, Selected);
                if (check == PaintResult.NotYourLand || check == PaintResult.UnderBuilding) { RouteValid = false; break; }
            }

            ShowRoutePreview(_route, RouteValid);
        }

        /// <summary>Green or red cells along the route and the flag at its end (also used by the editor preview).</summary>
        public void ShowRoutePreview(IReadOnlyList<GridCoord> cells, bool valid)
        {
            for (int i = 0; i < Mathf.Max(cells.Count, _markers.Count); i++)
            {
                if (i >= _markers.Count) _markers.Add(CreateMarker(i));
                var marker = _markers[i];
                marker.enabled = i < cells.Count;
                if (!marker.enabled) continue;
                marker.transform.position = grid.CellCenterWorld(cells[i]);
                marker.color = valid ? RouteOk : RouteBad;
            }

            if (_flag == null && flagSprite != null)
            {
                var go = new GameObject("RouteFlag");
                go.transform.SetParent(transform, false);
                _flag = go.AddComponent<SpriteRenderer>();
                _flag.sprite = flagSprite;
                _flag.sortingLayerName = SortingLayers.WorldUI;
            }

            if (_flag != null)
            {
                _flag.enabled = cells.Count > 0;
                if (cells.Count > 0) _flag.transform.position = grid.CellCenterWorld(cells[cells.Count - 1]);
            }
        }

        private SpriteRenderer CreateMarker(int index)
        {
            var go = new GameObject($"RouteCell_{index}");
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = cellSprite;
            r.sortingLayerName = SortingLayers.GroundOverlay;
            r.enabled = false;
            return r;
        }

        // ------------------------------------------------------------------ Input

        private void OnTapped(Vector2 screen)
        {
            if (!IsPainting) return;
            var cell = CellAt(screen);
            if (DrawsRoutes)
            {
                // First tap: start (and end) of a new road. With a road pending, a tap moves its flag.
                if (HasRoute) SetRoute(_routeStart.Value, cell);
                else SetRoute(cell, cell);
                return;
            }

            var result = Paint(cell);
            if (result == PaintResult.UnderBuilding) Notification?.Invoke("Water and roads can't go under a building.");
            else if (result == PaintResult.NotYourLand) Notification?.Invoke("You can only change your own land.");
            StateChanged?.Invoke();
        }

        public bool TryBeginDrag(Vector2 screenPosition)
        {
            if (!IsPainting) return false;
            var cell = CellAt(screenPosition);
            _lastCell = cell;
            if (DrawsRoutes)
            {
                // Grabbing the flag (or next to it) moves the end; anywhere else starts a new road.
                bool nearFlag = HasRoute && Math.Abs(cell.X - _routeEnd.X) <= 1 && Math.Abs(cell.Y - _routeEnd.Y) <= 1;
                if (nearFlag) SetRoute(_routeStart.Value, cell);
                else SetRoute(cell, cell);
                return true;
            }

            _edit.BeginStroke();
            Paint(cell);
            return true;
        }

        public void Drag(Vector2 screenPosition, Vector2 screenDelta)
        {
            var cell = CellAt(screenPosition);
            if (DrawsRoutes)
            {
                if (HasRoute && !cell.Equals(_routeEnd)) SetRoute(_routeStart.Value, cell);
                _lastCell = cell;
                return;
            }

            foreach (var c in GridLine.Between(_lastCell ?? cell, cell)) Paint(c);
            _lastCell = cell;
        }

        public void EndDrag(Vector2 screenPosition)
        {
            _lastCell = null;
            if (DrawsRoutes || _edit == null) return;
            _edit.EndStroke();
            StateChanged?.Invoke();
        }

        private GridCoord CellAt(Vector2 screen) => grid.ScreenToCell(_camera != null ? _camera : Camera.main, screen);

        // ------------------------------------------------------------------ Tiles

        private bool Editable(GridCoord cell) =>
            _town.Bounds.Contains(cell) && cell.X != railColumn && !railTilemap.HasTile(V(cell)) && (_land == null || _land.IsCellUnlocked(cell));

        /// <summary>What a cell looks like now, read from the tilemaps.</summary>
        public Terrain CurrentTerrain(GridCoord cell)
        {
            var v = V(cell);
            if (waterTilemap.HasTile(v)) return Terrain.Water;
            var road = roadTilemap.GetTile(v);
            if (road != null)
            {
                for (int i = 0; i < tiles.Length; i++)
                    if (tiles[i] == road && TerrainRules.IsRoad((Terrain)i)) return (Terrain)i;
                return Terrain.Road;
            }

            var ground = groundTilemap.GetTile(v);
            if (ground == null) return Terrain.Water;
            for (int i = 0; i < tiles.Length; i++)
            {
                var t = (Terrain)i;
                if (t != Terrain.Grass && !TerrainRules.IsBlocking(t) && tiles[i] == ground) return t;
            }

            return Terrain.Grass; // grass in any season
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
            bool hadRoad = roadTilemap.HasTile(v);
            if (terrain == Terrain.Water)
            {
                groundTilemap.SetTile(v, null);
                roadTilemap.SetTile(v, null);
                waterTilemap.SetTile(v, TileFor(Terrain.Water));
            }
            else if (TerrainRules.IsRoad(terrain))
            {
                waterTilemap.SetTile(v, null);
                groundTilemap.SetTile(v, grass);
                roadTilemap.SetTile(v, TileFor(terrain));
            }
            else
            {
                waterTilemap.SetTile(v, null);
                roadTilemap.SetTile(v, null);
                groundTilemap.SetTile(v, terrain == Terrain.Grass ? grass : TileFor(terrain));
            }

            // Roads next door join up (or get their kerb back).
            if (hadRoad || TerrainRules.IsRoad(terrain))
                foreach (var offset in new[] { Vector3Int.right, Vector3Int.up, Vector3Int.left, Vector3Int.down })
                    roadTilemap.RefreshTile(v + offset);
        }

        private static Vector3Int V(GridCoord c) => new Vector3Int(c.X, c.Y, 0);
    }
}
