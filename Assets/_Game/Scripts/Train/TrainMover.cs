using UnityEngine;
using TownBuilder.Domain;

namespace TownBuilder
{
    /// <summary>
    /// Drives the train along the railway: when a train arrives during play it rolls in from the map edge and slows down
    /// at the station (the station keeps its empty platform until then), and when it is sent off it pulls away and leaves
    /// over the far edge. Purely visual: the train's state lives in <see cref="Train"/> / <see cref="TrainController"/>.
    /// The railway is one grid column; the train travels towards +Y (screen up-left), locomotive first.
    /// </summary>
    public sealed class TrainMover : MonoBehaviour
    {
        [SerializeField] private IsoGrid grid;
        [SerializeField] private TrainController train;
        [Tooltip("Whole train (locomotive + wagons) driving towards the upper left. Pivot = bottom corner of its 1×N footprint.")]
        [SerializeField] private Sprite trainSprite;
        [SerializeField] private int railColumn = 9;
        [Tooltip("Grid Y of the train's rear end when it stands at the platform.")]
        [SerializeField] private float stopY = 17f;
        [SerializeField] private float enterFromY = -7f;
        [SerializeField] private float exitToY = 42f;
        [SerializeField] private float travelSeconds = 5f;

        private enum Motion { None, Arriving, Departing }

        private SpriteRenderer _renderer;
        private Motion _motion;
        private float _fromY, _toY, _elapsed, _y;

        /// <summary>Seconds for arriving or leaving (tests make it short).</summary>
        public float TravelSeconds
        {
            get => travelSeconds;
            set => travelSeconds = Mathf.Max(0.01f, value);
        }

        public bool IsMoving => _motion != Motion.None;
        public bool IsArriving => _motion == Motion.Arriving;
        public bool IsDeparting => _motion == Motion.Departing;
        public float CurrentY => _y;

        /// <summary>Call after <see cref="TrainController.Initialize"/>: a train already at the station on load just stands there.</summary>
        public void Initialize()
        {
            if (train == null) return;
            train.Arrived -= OnArrived;
            train.Departed -= OnDeparted;
            train.Arrived += OnArrived;
            train.Departed += OnDeparted;
        }

        private void OnDestroy()
        {
            if (train == null) return;
            train.Arrived -= OnArrived;
            train.Departed -= OnDeparted;
        }

        private void OnArrived()
        {
            if (trainSprite == null) return;
            train.HoldEmptyPlatform = true; // the station shows its train only once ours has stopped
            Begin(Motion.Arriving, enterFromY, stopY);
        }

        private void OnDeparted(bool full)
        {
            if (trainSprite == null) return;
            train.HoldEmptyPlatform = false;
            Begin(Motion.Departing, _motion == Motion.Arriving ? _y : stopY, exitToY);
        }

        private void Begin(Motion motion, float from, float to)
        {
            EnsureRenderer();
            _motion = motion;
            _fromY = from;
            _toY = to;
            _elapsed = 0f;
            _renderer.enabled = true;
            Place(from);
        }

        private void Update()
        {
            if (_motion == Motion.None) return;
            _elapsed += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_elapsed / travelSeconds);
            // Arriving: slow down into the station. Departing: pull away slowly, then speed up.
            float eased = _motion == Motion.Arriving ? 1f - (1f - k) * (1f - k) * (1f - k) : k * k * k;
            Place(Mathf.Lerp(_fromY, _toY, eased));
            if (k < 1f) return;

            if (_motion == Motion.Arriving) train.HoldEmptyPlatform = false;
            _motion = Motion.None;
            _renderer.enabled = false;
        }

        /// <summary>Editor previews: shows the train standing on the rails at grid Y <paramref name="y"/>.</summary>
        public void PreviewAt(float y)
        {
            if (trainSprite == null) return;
            EnsureRenderer();
            _renderer.enabled = true;
            Place(y);
        }

        private void Place(float y)
        {
            _y = y;
            var origin = grid.FootprintAnchorWorld(new GridCoord(railColumn, 0));
            var world = (Vector2)origin + grid.AxisY * y;
            _renderer.transform.position = new Vector3(world.x, world.y, 0f);
        }

        private void EnsureRenderer()
        {
            if (_renderer != null) return;
            var go = new GameObject("MovingTrain");
            go.transform.SetParent(transform, false);
            _renderer = go.AddComponent<SpriteRenderer>();
            _renderer.sprite = trainSprite;
            _renderer.sortingLayerName = SortingLayers.Objects;
            _renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            _renderer.enabled = false;
        }
    }
}
