using UnityEngine;

namespace TownBuilder
{
    /// <summary>
    /// The yellow up arrow above a building while a finger is held on it: it fades in and stretches upward until the hold is
    /// long enough to pick the building up (v0.42.0). One instance, created by <see cref="PlacementSystem"/>.
    /// </summary>
    public sealed class LiftCue : MonoBehaviour
    {
        private SpriteRenderer _renderer;

        private void Awake() => EnsureRenderer();

        private void EnsureRenderer()
        {
            if (_renderer != null) return;
            _renderer = gameObject.AddComponent<SpriteRenderer>();
            _renderer.sprite = CueSprites.UpArrow;
            _renderer.sortingLayerName = SortingLayers.Objects;
            _renderer.sortingOrder = 2000;
            _renderer.enabled = false;
        }

        public bool IsShown => _renderer != null && _renderer.enabled;

        /// <summary>Shows the arrow above the building; <paramref name="progress"/> is 0-1 of the hold time.</summary>
        public void Show(BuildingView view, float progress)
        {
            EnsureRenderer();
            if (view == null || view.SpriteRenderer == null) { Hide(); return; }
            float k = Mathf.Clamp01((progress - 0.15f) / 0.85f);
            var bounds = view.SpriteRenderer.bounds;
            float bob = MotionPreferences.ReduceMotion ? 0f : Mathf.Sin(Time.unscaledTime * 9f) * 0.04f;
            transform.position = new Vector3(bounds.center.x, bounds.max.y - bounds.size.y * 0.12f + bob, 0f);
            float stretch = MotionPreferences.ReduceMotion ? 1f : 0.45f + 0.55f * k;
            transform.localScale = new Vector3(0.85f + 0.15f * k, stretch, 1f);
            _renderer.color = new Color(1f, 1f, 1f, Mathf.Clamp01(k * 1.6f));
            _renderer.enabled = k > 0f;
        }

        public void Hide()
        {
            if (_renderer != null) _renderer.enabled = false;
        }
    }
}
