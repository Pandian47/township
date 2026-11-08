using System;
using TownBuilder.Domain;
using UnityEngine;

namespace TownBuilder
{
    /// <summary>
    /// Player interaction with buildings: select, place new (with preview), move, remove.
    ///
    ///   Idle --tap building--> Selected --Move--> Moving --Confirm/Cancel--> Selected
    ///   Idle --shop item-----> PlacingNew --Confirm/Cancel--> Idle
    ///
    /// During PlacingNew/Moving the preview follows taps, and can be dragged (drag starting on the preview).
    /// All rules (bounds, overlap, terrain) are evaluated by the domain <see cref="Town"/>.
    /// </summary>
    public sealed class PlacementSystem : MonoBehaviour, IPointerDragHandler
    {
        public enum Mode
        {
            Idle,
            Selected,
            PlacingNew,
            Moving,
        }

        [SerializeField] private Camera worldCamera;
        [SerializeField] private PointerInput pointerInput;
        [SerializeField] private IsoGrid grid;
        [SerializeField] private TownBuildingController buildings;
        [SerializeField] private FootprintHighlighter highlighter;

        [Header("Feedback colours")]
        [SerializeField] private Color previewValidTint = new Color(1f, 1f, 1f, 0.85f);
        [SerializeField] private Color previewInvalidTint = new Color(1f, 0.55f, 0.55f, 0.75f);
        [SerializeField] private Color selectedTint = new Color(1f, 1f, 0.85f, 1f);
        [SerializeField] private Color cellValidColor = new Color(0.35f, 1f, 0.45f, 0.55f);
        [SerializeField] private Color cellInvalidColor = new Color(1f, 0.3f, 0.3f, 0.6f);
        [SerializeField] private Color cellSelectedColor = new Color(1f, 0.95f, 0.4f, 0.45f);

        [Tooltip("When a new building is picked from the shop, search this many cells around the screen centre for a free spot.")]
        [SerializeField] private int freeSpotSearchRadius = 12;

        private Town _town;
        private Wallet _wallet;
        private PlayerProgress _progress;
        private GameContent _content;

        private BuildingDefinition _definition;
        private BuildingView _preview;          // New building preview, or the real view of the building being moved.
        private string _targetInstanceId;       // Selected / moving building.
        private GridCoord _previewOrigin;
        private GridCoord _moveStartOrigin;
        private GridCoord _dragCellOffset;

        public event Action StateChanged;

        /// <summary>A tap in Idle/Selected mode that hit no building (the cell under the finger).</summary>
        public event Action<GridCoord> GroundTapped;

        /// <summary>
        /// Optional hook asked when a placed building is tapped in Idle/Selected mode. Return true to consume the tap
        /// (e.g. farming harvests a ready field instead of selecting it).
        /// </summary>
        public void AddTapInterceptor(Func<PlacedBuilding, bool> interceptor)
        {
            if (interceptor != null && !_tapInterceptors.Contains(interceptor)) _tapInterceptors.Add(interceptor);
        }

        private readonly System.Collections.Generic.List<Func<PlacedBuilding, bool>> _tapInterceptors =
            new System.Collections.Generic.List<Func<PlacedBuilding, bool>>();

        private bool InterceptTap(PlacedBuilding building)
        {
            foreach (var interceptor in _tapInterceptors)
                if (interceptor(building)) return true;
            return false;
        }

        /// <summary>Raised whenever the town layout or wallet changed because of a player action (autosave hook).</summary>
        public event Action TownModified;

        /// <summary>A NEW building was bought and placed (not raised for loaded or moved buildings).</summary>
        public event Action<PlacedBuilding> BuildingPlaced;

        public Mode CurrentMode { get; private set; } = Mode.Idle;
        public PlacementResult PreviewValidity { get; private set; } = PlacementResult.Valid;
        public BuildingDefinition ActiveDefinition => _definition;
        public string TargetInstanceId => _targetInstanceId;
        public int DragPriority => 100;

        public bool CanAffordActive => _definition != null && _wallet != null && _wallet.CanAffordCoins(_definition.CoinCost);

        public bool CanConfirm =>
            (CurrentMode == Mode.PlacingNew && PreviewValidity == PlacementResult.Valid && CanAffordActive) ||
            (CurrentMode == Mode.Moving && PreviewValidity == PlacementResult.Valid);

        public void Initialize(Town town, Wallet wallet, PlayerProgress progress, GameContent content)
        {
            _town = town;
            _wallet = wallet;
            _progress = progress;
            _content = content;
            EnterIdle();
        }

        private void OnEnable()
        {
            if (pointerInput == null) return;
            pointerInput.RegisterDragHandler(this);
            pointerInput.Tapped += OnTapped;
        }

        private void OnDisable()
        {
            if (pointerInput == null) return;
            pointerInput.UnregisterDragHandler(this);
            pointerInput.Tapped -= OnTapped;
        }

        // ---------------------------------------------------------------- Commands (called by UI)

        public bool BeginPlacement(BuildingDefinition definition)
        {
            if (_town == null || definition == null) return false;
            if (!_progress.IsUnlocked(definition.UnlockLevel)) return false;
            if (!CanBuildMore(definition)) return false;

            CancelActive();
            ClearSelectionTint();
            _definition = definition;
            _targetInstanceId = null;
            _preview = buildings.CreatePreview(definition);

            var size = definition.FootprintSize;
            var centerCell = grid.ScreenToCell(worldCamera, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            var start = _town.Bounds.ClampOrigin(new GridCoord(centerCell.X - size.X / 2, centerCell.Y - size.Y / 2), size);
            _previewOrigin = FindNearestFreeOrigin(start, size, null);

            SetMode(Mode.PlacingNew);
            RefreshPreview();
            return true;
        }

        public bool BeginMoveSelected()
        {
            if (CurrentMode != Mode.Selected || !_town.TryGet(_targetInstanceId, out var building)) return false;
            if (!buildings.TryGetView(building.InstanceId, out var view)) return false;

            _preview = view;
            _moveStartOrigin = building.Origin;
            _previewOrigin = building.Origin;
            SetMode(Mode.Moving);
            RefreshPreview();
            return true;
        }

        public bool Confirm()
        {
            if (!CanConfirm) return false;

            if (CurrentMode == Mode.PlacingNew)
            {
                var definition = _definition;
                if (!_wallet.TrySpendCoins(definition.CoinCost)) return false;

                string id = Guid.NewGuid().ToString("N");
                var result = _town.TryAdd(id, definition.Id, _previewOrigin, definition.FootprintSize, out var placed);
                if (result != PlacementResult.Valid)
                {
                    _wallet.AddCoins(definition.CoinCost); // refund; should not happen because CanConfirm checked it
                    Debug.LogWarning($"[Placement] Unexpected placement failure: {result}");
                    return false;
                }

                DestroyPreview();
                EnterIdle();
                BuildingPlaced?.Invoke(placed);
                TownModified?.Invoke();
                return true;
            }

            if (CurrentMode == Mode.Moving)
            {
                var result = _town.TryMove(_targetInstanceId, _previewOrigin);
                if (result != PlacementResult.Valid) return false;
                string id = _targetInstanceId;
                _preview = null;
                SelectBuilding(id);
                TownModified?.Invoke();
                return true;
            }

            return false;
        }

        /// <summary>Cancels the current placement/move, or clears the selection.</summary>
        public void Cancel()
        {
            if (CurrentMode == Mode.Moving)
            {
                string id = _targetInstanceId;
                CancelActive();
                SelectBuilding(id);
                return;
            }

            CancelActive();
            EnterIdle();
        }

        public bool RemoveSelected()
        {
            if (CurrentMode != Mode.Selected || !_town.TryGet(_targetInstanceId, out var building)) return false;
            if (_content.TryGetBuilding(building.DefinitionId, out var definition) && !definition.Removable) return false;

            _town.Remove(building.InstanceId);
            EnterIdle();
            TownModified?.Invoke();
            return true;
        }

        /// <summary>False when the player already owns <see cref="BuildingDefinition.MaxCount"/> of this building.</summary>
        public bool CanBuildMore(BuildingDefinition definition) =>
            definition.MaxCount <= 0 || _town == null || _town.CountOf(definition.Id) < definition.MaxCount;

        public bool TryGetTargetDefinition(out BuildingDefinition definition)
        {
            definition = null;
            if (CurrentMode == Mode.PlacingNew) { definition = _definition; return definition != null; }
            return _town != null && _town.TryGet(_targetInstanceId, out var b) && _content.TryGetBuilding(b.DefinitionId, out definition);
        }

        // ---------------------------------------------------------------- Pointer input

        private void OnTapped(Vector2 screenPosition)
        {
            if (_town == null) return;

            switch (CurrentMode)
            {
                case Mode.PlacingNew:
                case Mode.Moving:
                {
                    var size = PreviewSize;
                    var cell = grid.ScreenToCell(worldCamera, screenPosition);
                    SetPreviewOrigin(new GridCoord(cell.X - size.X / 2, cell.Y - size.Y / 2));
                    break;
                }

                default:
                {
                    var world = grid.ScreenToWorld(worldCamera, screenPosition);
                    if (buildings.TryPick(world, out var building))
                    {
                        if (InterceptTap(building))
                        {
                            if (CurrentMode == Mode.Selected) EnterIdle();
                        }
                        else
                        {
                            SelectBuilding(building.InstanceId);
                        }
                    }
                    else
                    {
                        EnterIdle();
                        GroundTapped?.Invoke(grid.WorldToCell(world));
                    }
                    break;
                }
            }
        }

        public bool TryBeginDrag(Vector2 screenPosition)
        {
            if (_town == null || _preview == null) return false;
            if (CurrentMode != Mode.PlacingNew && CurrentMode != Mode.Moving) return false;

            var world = grid.ScreenToWorld(worldCamera, screenPosition);
            var cell = grid.WorldToCell(world);
            var size = PreviewSize;
            bool onFootprint = cell.X >= _previewOrigin.X && cell.X < _previewOrigin.X + size.X &&
                               cell.Y >= _previewOrigin.Y && cell.Y < _previewOrigin.Y + size.Y;
            if (!onFootprint && !_preview.HitTest(world, grid.AxisX, grid.AxisY)) return false;

            // Keep the grab point under the finger while dragging.
            _dragCellOffset = onFootprint ? cell - _previewOrigin : new GridCoord(size.X / 2, size.Y / 2);
            return true;
        }

        public void Drag(Vector2 screenPosition, Vector2 screenDelta)
        {
            var cell = grid.ScreenToCell(worldCamera, screenPosition);
            SetPreviewOrigin(cell - _dragCellOffset);
        }

        public void EndDrag(Vector2 screenPosition) { }

        // ---------------------------------------------------------------- Internals

        private GridCoord PreviewSize => _definition != null ? _definition.FootprintSize : GridCoord.One;

        /// <summary>Moves the placement/move preview (clamped to the map). Used by taps, drags and tutorials.</summary>
        public void SetPreviewOrigin(GridCoord origin)
        {
            if (_town == null || _preview == null) return;
            origin = _town.Bounds.ClampOrigin(origin, PreviewSize);
            if (origin == _previewOrigin) return;
            _previewOrigin = origin;
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            if (_preview == null) return;
            string ignore = CurrentMode == Mode.Moving ? _targetInstanceId : null;
            PreviewValidity = _town.CanPlace(_previewOrigin, PreviewSize, ignore);
            bool valid = PreviewValidity == PlacementResult.Valid;

            _preview.SetAnchor(grid.FootprintAnchorWorld(_previewOrigin));
            _preview.SetTint(valid ? previewValidTint : previewInvalidTint);
            highlighter.Show(_previewOrigin, PreviewSize, valid ? cellValidColor : cellInvalidColor);
            StateChanged?.Invoke();
        }

        /// <summary>Selects a placed building (cancels any placement in progress).</summary>
        public void SelectBuilding(string instanceId)
        {
            if (_town == null) return;
            if (CurrentMode == Mode.PlacingNew || CurrentMode == Mode.Moving) CancelActive();
            if (!_town.TryGet(instanceId, out var building)) { EnterIdle(); return; }

            ClearSelectionTint();
            _targetInstanceId = instanceId;
            _content.TryGetBuilding(building.DefinitionId, out _definition);
            if (buildings.TryGetView(instanceId, out var view)) view.SetTint(selectedTint);
            highlighter.Show(building.Origin, building.Size, cellSelectedColor);
            SetMode(Mode.Selected);
        }

        private void EnterIdle()
        {
            ClearSelectionTint();
            _targetInstanceId = null;
            _definition = null;
            PreviewValidity = PlacementResult.Valid;
            if (highlighter != null) highlighter.Hide();
            SetMode(Mode.Idle);
        }

        private void CancelActive()
        {
            if (CurrentMode == Mode.PlacingNew)
            {
                DestroyPreview();
            }
            else if (CurrentMode == Mode.Moving && _preview != null)
            {
                _preview.SetAnchor(grid.FootprintAnchorWorld(_moveStartOrigin));
                _preview.SetTint(Color.white);
                _preview = null;
            }
        }

        private void DestroyPreview()
        {
            if (_preview != null) Destroy(_preview.gameObject);
            _preview = null;
        }

        private void ClearSelectionTint()
        {
            if (_targetInstanceId != null && buildings != null && buildings.TryGetView(_targetInstanceId, out var view))
                view.SetTint(Color.white);
        }

        private void SetMode(Mode mode)
        {
            CurrentMode = mode;
            StateChanged?.Invoke();
        }

        /// <summary>Spiral search for the closest origin where the footprint fits; falls back to <paramref name="start"/>.</summary>
        private GridCoord FindNearestFreeOrigin(GridCoord start, GridCoord size, string ignoreId)
        {
            if (_town.CanPlace(start, size, ignoreId) == PlacementResult.Valid) return start;
            for (int r = 1; r <= freeSpotSearchRadius; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dy = -r; dy <= r; dy++)
                    {
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue; // ring only
                        var candidate = new GridCoord(start.X + dx, start.Y + dy);
                        if (_town.CanPlace(candidate, size, ignoreId) == PlacementResult.Valid) return candidate;
                    }
                }
            }

            return start;
        }
    }
}
