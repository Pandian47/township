using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using ETouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace TownBuilder
{
    /// <summary>
    /// Receives drag gestures from <see cref="PointerInput"/>. Handlers are asked in descending
    /// <see cref="DragPriority"/> order; the first one returning true from TryBeginDrag owns the whole drag.
    /// </summary>
    public interface IPointerDragHandler
    {
        int DragPriority { get; }
        bool TryBeginDrag(Vector2 screenPosition);
        void Drag(Vector2 screenPosition, Vector2 screenDelta);
        void EndDrag(Vector2 screenPosition);
    }

    /// <summary>
    /// Turns raw touch (device) and mouse (Editor) input into world gestures: tap, drag, pinch zoom, pinch pan.
    /// Gestures that start on top of UI are ignored entirely, so panels never move the camera.
    /// </summary>
    public sealed class PointerInput : MonoBehaviour
    {
        [Tooltip("Movement (in inches) before a press becomes a drag instead of a tap.")]
        [SerializeField] private float dragThresholdInches = 0.06f;
        [SerializeField] private float minDragThresholdPixels = 8f;
        [Tooltip("Zoom factor applied per mouse wheel notch in the Editor.")]
        [SerializeField] private float mouseWheelZoomStep = 0.1f;

        private readonly List<IPointerDragHandler> _dragHandlers = new List<IPointerDragHandler>();
        private readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
        private PointerEventData _pointerEventData;
        private EventSystem _pointerEventSystem;

        private bool _pressed;
        private bool _startedOverUI;
        private bool _gestureCancelled;
        private bool _dragging;
        private bool _pinching;
        private Vector2 _pressPosition;
        private Vector2 _lastPosition;
        private Vector2 _lastPinchCenter;
        private float _lastPinchDistance;
        private IPointerDragHandler _activeDragHandler;

        /// <summary>Screen position of a short press without movement.</summary>
        public event Action<Vector2> Tapped;

        /// <summary>(factor, screen focus point). factor &lt; 1 zooms in, &gt; 1 zooms out (multiplies orthographic size).</summary>
        public event Action<float, Vector2> ZoomRequested;

        /// <summary>Screen-space movement of the two-finger centre while pinching.</summary>
        public event Action<Vector2> PinchPanned;

        public bool IsGestureActive => _pressed;

        public void RegisterDragHandler(IPointerDragHandler handler)
        {
            if (handler == null || _dragHandlers.Contains(handler)) return;
            _dragHandlers.Add(handler);
            _dragHandlers.Sort((a, b) => b.DragPriority.CompareTo(a.DragPriority));
        }

        public void UnregisterDragHandler(IPointerDragHandler handler)
        {
            if (_activeDragHandler == handler) _activeDragHandler = null;
            _dragHandlers.Remove(handler);
        }

        private void OnEnable() => EnhancedTouchSupport.Enable();

        private void OnDisable()
        {
            if (_dragging && _activeDragHandler != null) _activeDragHandler.EndDrag(_lastPosition);
            ResetGesture();
            EnhancedTouchSupport.Disable();
        }

        private void Update()
        {
            int count = 0;
            Vector2 p0 = default, p1 = default;
            int id0 = int.MaxValue, id1 = int.MaxValue;

            var touches = ETouch.activeTouches;
            if (touches.Count > 0)
            {
                // Pick the two lowest touch ids that are still down so the primary finger stays stable.
                for (int i = 0; i < touches.Count; i++)
                {
                    var t = touches[i];
                    if (t.phase == ETouchPhase.Ended || t.phase == ETouchPhase.Canceled) continue;
                    count++;
                    if (t.touchId < id0) { id1 = id0; p1 = p0; id0 = t.touchId; p0 = t.screenPosition; }
                    else if (t.touchId < id1) { id1 = t.touchId; p1 = t.screenPosition; }
                }
            }
            else if (Mouse.current != null)
            {
                var mouse = Mouse.current;
                if (mouse.leftButton.isPressed)
                {
                    count = 1;
                    p0 = mouse.position.ReadValue();
                }

                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f && !_pressed)
                {
                    var mousePos = mouse.position.ReadValue();
                    if (!IsOverUI(mousePos))
                        ZoomRequested?.Invoke(wheel > 0 ? 1f - mouseWheelZoomStep : 1f + mouseWheelZoomStep, mousePos);
                }
            }

            Step(Mathf.Min(count, 2), p0, p1);
        }

        private void Step(int count, Vector2 p0, Vector2 p1)
        {
            if (count == 0)
            {
                if (_pressed) Release();
                return;
            }

            if (!_pressed)
            {
                _pressed = true;
                _pressPosition = _lastPosition = p0;
                _startedOverUI = IsOverUI(p0);
                if (count == 2 && !_startedOverUI) BeginPinch(p0, p1);
                return;
            }

            if (_startedOverUI) return;

            if (count == 2)
            {
                if (!_pinching)
                {
                    EndDragIfActive();
                    BeginPinch(p0, p1);
                }
                else
                {
                    UpdatePinch(p0, p1);
                }

                return;
            }

            if (_pinching)
            {
                // One finger lifted after a pinch: ignore the rest of this gesture.
                _pinching = false;
                _gestureCancelled = true;
            }

            if (_gestureCancelled)
            {
                _lastPosition = p0;
                return;
            }

            if (!_dragging)
            {
                if ((p0 - _pressPosition).magnitude >= DragThresholdPixels)
                {
                    _dragging = true;
                    _activeDragHandler = FindDragHandler(_pressPosition);
                    _activeDragHandler?.Drag(p0, p0 - _lastPosition);
                }
            }
            else
            {
                var delta = p0 - _lastPosition;
                if (delta.sqrMagnitude > 0f) _activeDragHandler?.Drag(p0, delta);
            }

            _lastPosition = p0;
        }

        private void Release()
        {
            if (!_startedOverUI && !_gestureCancelled && !_pinching)
            {
                if (_dragging) _activeDragHandler?.EndDrag(_lastPosition);
                else Tapped?.Invoke(_lastPosition);
            }

            ResetGesture();
        }

        private void ResetGesture()
        {
            _pressed = false;
            _startedOverUI = false;
            _gestureCancelled = false;
            _dragging = false;
            _pinching = false;
            _activeDragHandler = null;
        }

        private void EndDragIfActive()
        {
            if (_dragging) _activeDragHandler?.EndDrag(_lastPosition);
            _dragging = false;
            _activeDragHandler = null;
        }

        private void BeginPinch(Vector2 p0, Vector2 p1)
        {
            _pinching = true;
            _lastPinchDistance = Mathf.Max(1f, Vector2.Distance(p0, p1));
            _lastPinchCenter = (p0 + p1) * 0.5f;
        }

        private void UpdatePinch(Vector2 p0, Vector2 p1)
        {
            float distance = Mathf.Max(1f, Vector2.Distance(p0, p1));
            var center = (p0 + p1) * 0.5f;

            var panDelta = center - _lastPinchCenter;
            if (panDelta.sqrMagnitude > 0f) PinchPanned?.Invoke(panDelta);

            float factor = _lastPinchDistance / distance;
            if (Mathf.Abs(factor - 1f) > 0.0001f) ZoomRequested?.Invoke(factor, center);

            _lastPinchDistance = distance;
            _lastPinchCenter = center;
        }

        private IPointerDragHandler FindDragHandler(Vector2 screenPosition)
        {
            foreach (var handler in _dragHandlers)
                if (handler.TryBeginDrag(screenPosition)) return handler;
            return null;
        }

        private float DragThresholdPixels =>
            Screen.dpi > 0f ? Mathf.Max(minDragThresholdPixels, Screen.dpi * dragThresholdInches) : minDragThresholdPixels * 1.5f;

        private bool IsOverUI(Vector2 screenPosition)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return false;
            if (_pointerEventData == null || _pointerEventSystem != eventSystem)
            {
                _pointerEventData = new PointerEventData(eventSystem);
                _pointerEventSystem = eventSystem;
            }

            _pointerEventData.position = screenPosition;
            _uiHits.Clear();
            eventSystem.RaycastAll(_pointerEventData, _uiHits);
            return _uiHits.Count > 0;
        }
    }
}
