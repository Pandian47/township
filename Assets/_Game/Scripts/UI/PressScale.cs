using UnityEngine;
using UnityEngine.EventSystems;

namespace TownBuilder
{
    /// <summary>Buttons squash a little while pressed and spring back on release (added to every button by ButtonSounds).</summary>
    public sealed class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private const float PressedScale = 0.93f;
        private const float Speed = 18f;

        private Vector3 _base = Vector3.one;
        private float _target = 1f;
        private float _current = 1f;

        public bool IsPressed => _target < 1f;

        private void Awake() => _base = transform.localScale;

        public void OnPointerDown(PointerEventData eventData) => _target = PressedScale;
        public void OnPointerUp(PointerEventData eventData) => _target = 1f;
        public void OnPointerExit(PointerEventData eventData) => _target = 1f;

        private void OnDisable()
        {
            _target = _current = 1f;
            transform.localScale = _base;
        }

        private void Update()
        {
            if (Mathf.Approximately(_current, _target)) return;
            // Exponential approach: fast at first, settles smoothly.
            _current = Mathf.Lerp(_current, _target, 1f - Mathf.Exp(-Speed * Time.unscaledDeltaTime));
            if (Mathf.Abs(_current - _target) < 0.002f) _current = _target;
            transform.localScale = _base * _current;
        }
    }
}
