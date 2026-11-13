using UnityEngine;
using UnityEngine.UI;

namespace TownBuilder
{
    /// <summary>
    /// v0.41.1: the conveyor belt under the baskets in the production window. A tiled strip (one tile wider than the
    /// belt, clipped by the belt's mask) slides left tile by tile while the factory is making something, so the belt
    /// looks like it is running. Stands still with Reduce motion.
    /// </summary>
    public sealed class BeltScroller : MonoBehaviour
    {
        [Tooltip("The tiled belt picture; its parent clips it.")]
        [SerializeField] private RectTransform strip;
        [Tooltip("Reference pixels per second.")]
        [SerializeField] private float speed = 48f;

        private float _tile = 75f;
        private float _offset;

        /// <summary>True while the belt should run (something is being made).</summary>
        public bool Running { get; set; }

        /// <summary>How far the belt has slid within one tile (tests read it).</summary>
        public float Offset => _offset;

        private void OnEnable() => Setup();

        private void Setup()
        {
            if (strip == null) return;
            var image = strip.GetComponent<Image>();
            if (image != null && image.sprite != null && image.pixelsPerUnit > 0f) _tile = image.sprite.rect.width / image.pixelsPerUnit;
            if (_tile < 1f) _tile = 75f;
            strip.offsetMax = new Vector2(_tile, strip.offsetMax.y); // one extra tile so the slide never shows a gap
        }

        private void Update()
        {
            if (strip == null || !Running || MotionPreferences.ReduceMotion) return;
            _offset = (_offset + speed * Time.unscaledDeltaTime) % _tile;
            strip.anchoredPosition = new Vector2(-_offset, 0f);
        }
    }
}
