using UnityEngine;

namespace TownBuilder
{
    /// <summary>
    /// Mobile orthographic camera: one-finger drag pans (with inertia), pinch / mouse wheel zooms around the focus point,
    /// movement and zoom are smoothed and clamped so the view never leaves the map.
    /// Put this on the CameraRig; the Camera itself can be the rig or a child.
    /// </summary>
    public sealed class IsoCameraController : MonoBehaviour, IPointerDragHandler
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private PointerInput pointerInput;

        [Header("Zoom (orthographic size)")]
        [Tooltip("Closest zoom (v0.38.3: 1.6, was 2.5). Smaller = closer.")]
        [SerializeField] private float minOrthographicSize = 1.6f;
        [SerializeField] private float maxOrthographicSize = 12f;
        [SerializeField] private float zoomSmoothTime = 0.08f;

        [Header("Movement")]
        [SerializeField] private float positionSmoothTime = 0.05f;
        [Tooltip("How fast fling inertia decays. Higher = stops sooner.")]
        [SerializeField] private float inertiaDamping = 5f;
        [SerializeField] private float maxInertiaSpeed = 40f;
        [Tooltip("Extra world units the view may extend past the map edge.")]
        [SerializeField] private float boundsPadding = 1.5f;

        private Rect _worldBounds;
        private bool _hasBounds;
        private Vector2 _targetPosition;
        private float _targetSize;
        private Vector3 _positionVelocity;
        private float _sizeVelocity;
        private Vector2 _inertia;
        private Vector2 _dragVelocity;
        private bool _dragging;
        private bool _initialized;

        public int DragPriority => 0; // Fallback: anything else (e.g. dragging a building) wins.

        public Camera TargetCamera => targetCamera;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = GetComponentInChildren<Camera>();
            targetCamera.orthographic = true;
            SyncTargets();
        }

        private void OnEnable()
        {
            if (pointerInput == null) return;
            pointerInput.RegisterDragHandler(this);
            pointerInput.ZoomRequested += OnZoomRequested;
            pointerInput.PinchPanned += OnPinchPanned;
        }

        private void OnDisable()
        {
            if (pointerInput == null) return;
            pointerInput.UnregisterDragHandler(this);
            pointerInput.ZoomRequested -= OnZoomRequested;
            pointerInput.PinchPanned -= OnPinchPanned;
        }

        public void SetWorldBounds(Rect worldBounds)
        {
            _worldBounds = worldBounds;
            _hasBounds = worldBounds.width > 0f && worldBounds.height > 0f;
            _targetSize = Mathf.Clamp(_targetSize, minOrthographicSize, EffectiveMaxSize());
            _targetPosition = ClampPosition(_targetPosition, _targetSize);
        }

        /// <summary>Jumps (no smoothing) to a world position, e.g. on scene start.</summary>
        public void SnapTo(Vector2 worldPosition, float? orthographicSize = null)
        {
            if (orthographicSize.HasValue) _targetSize = Mathf.Clamp(orthographicSize.Value, minOrthographicSize, EffectiveMaxSize());
            _targetPosition = ClampPosition(worldPosition, _targetSize);
            _inertia = Vector2.zero;
            ApplyImmediately();
        }

        public bool TryBeginDrag(Vector2 screenPosition)
        {
            _dragging = true;
            _inertia = Vector2.zero;
            _dragVelocity = Vector2.zero;
            return true;
        }

        public void Drag(Vector2 screenPosition, Vector2 screenDelta)
        {
            var worldDelta = ScreenDeltaToWorld(screenDelta);
            _targetPosition = ClampPosition(_targetPosition - worldDelta, _targetSize);

            float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            _dragVelocity = Vector2.Lerp(_dragVelocity, -worldDelta / dt, 0.5f);
        }

        public void EndDrag(Vector2 screenPosition)
        {
            _dragging = false;
            _inertia = Vector2.ClampMagnitude(_dragVelocity, maxInertiaSpeed);
        }

        private void OnPinchPanned(Vector2 screenDelta)
        {
            _inertia = Vector2.zero;
            _targetPosition = ClampPosition(_targetPosition - ScreenDeltaToWorld(screenDelta), _targetSize);
        }

        private void OnZoomRequested(float factor, Vector2 focusScreenPosition)
        {
            float newSize = Mathf.Clamp(_targetSize * factor, minOrthographicSize, EffectiveMaxSize());
            if (Mathf.Approximately(newSize, _targetSize)) return;

            // Keep the world point under the fingers/cursor fixed while zooming.
            var fromCenter = focusScreenPosition - new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var worldPerPixelChange = 2f * (_targetSize - newSize) / Mathf.Max(1, Screen.height);
            _targetPosition += fromCenter * worldPerPixelChange;
            _targetSize = newSize;
            _targetPosition = ClampPosition(_targetPosition, _targetSize);
        }

        private void LateUpdate()
        {
            if (!_initialized) SyncTargets();
            float dt = Time.unscaledDeltaTime;

            if (!_dragging && _inertia.sqrMagnitude > 0.0001f)
            {
                _targetPosition = ClampPosition(_targetPosition + _inertia * dt, _targetSize);
                _inertia *= Mathf.Exp(-inertiaDamping * dt);
                if (_inertia.sqrMagnitude < 0.01f) _inertia = Vector2.zero;
            }

            // Aspect ratio can change (rotation, split screen), so re-clamp every frame.
            _targetSize = Mathf.Clamp(_targetSize, minOrthographicSize, EffectiveMaxSize());
            _targetPosition = ClampPosition(_targetPosition, _targetSize);

            targetCamera.orthographicSize = Mathf.SmoothDamp(targetCamera.orthographicSize, _targetSize, ref _sizeVelocity,
                zoomSmoothTime, Mathf.Infinity, dt);

            var current = transform.position;
            var target = new Vector3(_targetPosition.x, _targetPosition.y, current.z);
            transform.position = Vector3.SmoothDamp(current, target, ref _positionVelocity, positionSmoothTime, Mathf.Infinity, dt);
        }

        private void SyncTargets()
        {
            if (targetCamera == null) return;
            _targetPosition = transform.position;
            _targetSize = targetCamera.orthographicSize;
            _initialized = true;
        }

        private void ApplyImmediately()
        {
            targetCamera.orthographicSize = _targetSize;
            transform.position = new Vector3(_targetPosition.x, _targetPosition.y, transform.position.z);
            _positionVelocity = Vector3.zero;
            _sizeVelocity = 0f;
        }

        private Vector2 ScreenDeltaToWorld(Vector2 screenDelta) =>
            screenDelta * (2f * targetCamera.orthographicSize / Mathf.Max(1, Screen.height));

        private Rect PaddedBounds()
        {
            var b = _worldBounds;
            return new Rect(b.xMin - boundsPadding, b.yMin - boundsPadding, b.width + boundsPadding * 2f, b.height + boundsPadding * 2f);
        }

        private float EffectiveMaxSize()
        {
            if (!_hasBounds) return maxOrthographicSize;
            var padded = PaddedBounds();
            float aspect = Mathf.Max(0.1f, targetCamera.aspect);
            float fit = Mathf.Min(padded.height * 0.5f, padded.width * 0.5f / aspect);
            return Mathf.Max(minOrthographicSize, Mathf.Min(maxOrthographicSize, fit));
        }

        private Vector2 ClampPosition(Vector2 position, float orthographicSize)
        {
            if (!_hasBounds) return position;
            var padded = PaddedBounds();
            float halfHeight = orthographicSize;
            float halfWidth = orthographicSize * targetCamera.aspect;

            float minX = padded.xMin + halfWidth, maxX = padded.xMax - halfWidth;
            float minY = padded.yMin + halfHeight, maxY = padded.yMax - halfHeight;

            position.x = minX > maxX ? padded.center.x : Mathf.Clamp(position.x, minX, maxX);
            position.y = minY > maxY ? padded.center.y : Mathf.Clamp(position.y, minY, maxY);
            return position;
        }
    }
}
