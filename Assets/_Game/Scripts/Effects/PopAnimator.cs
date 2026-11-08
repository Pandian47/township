using UnityEngine;

namespace TownBuilder
{
    /// <summary>
    /// Short scale "pop" on any transform (buildings, UI pills). Restarting while playing is fine.
    /// Squash: wide-and-short then tall-and-thin, settling back (for buildings; scales around the pivot = footprint corner).
    /// Uniform: grows and settles (for UI counters).
    /// </summary>
    public sealed class PopAnimator : MonoBehaviour
    {
        public enum Style
        {
            Squash,
            Uniform,
        }

        private Vector3 _baseScale;
        private bool _hasBase;
        private float _age = -1f;
        private float _duration;
        private float _strength;
        private Style _style;

        public bool IsPlaying => _age >= 0f;

        public static PopAnimator Play(Transform target, Style style = Style.Squash, float strength = 0.12f, float duration = 0.4f)
        {
            if (target == null) return null;
            var pop = target.GetComponent<PopAnimator>();
            if (pop == null) pop = target.gameObject.AddComponent<PopAnimator>();
            pop.Begin(style, strength, duration);
            return pop;
        }

        private void Begin(Style style, float strength, float duration)
        {
            if (!_hasBase)
            {
                _baseScale = transform.localScale;
                _hasBase = true;
            }

            _style = style;
            _strength = strength;
            _duration = Mathf.Max(0.05f, duration);
            _age = 0f;
            enabled = true;
        }

        private void Update()
        {
            if (_age < 0f) { enabled = false; return; }

            _age += Time.unscaledDeltaTime;
            float k = _age / _duration;
            if (k >= 1f)
            {
                Stop();
                return;
            }

            float decay = 1f - k;
            if (_style == Style.Squash)
            {
                float w = Mathf.Sin(k * Mathf.PI * 2f) * decay * _strength;
                transform.localScale = new Vector3(_baseScale.x * (1f + w), _baseScale.y * (1f - w), _baseScale.z);
            }
            else
            {
                float s = 1f + Mathf.Sin(k * Mathf.PI) * decay * _strength * 2f;
                transform.localScale = _baseScale * s;
            }
        }

        /// <summary>Ends the animation and restores the original scale.</summary>
        public void Stop()
        {
            if (_hasBase) transform.localScale = _baseScale;
            _age = -1f;
            enabled = false;
        }

        private void OnDisable()
        {
            if (_age >= 0f && _hasBase) transform.localScale = _baseScale;
            _age = -1f;
        }
    }
}
