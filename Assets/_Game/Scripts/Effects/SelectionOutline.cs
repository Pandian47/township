using System.Collections.Generic;
using UnityEngine;

namespace TownBuilder
{
    /// <summary>
    /// Glowing rim around the selected building. Made from offset copies of the building's sprites drawn as a flat
    /// colour (SpriteSilhouette shader), hidden inside the building's own shape by a SpriteMask, so only the rim shows.
    /// The rim is drawn on the Effects layer, so it stays visible even behind other buildings.
    /// Works with any sprite (placeholder or final art) without extra textures or import settings.
    /// </summary>
    public sealed class SelectionOutline : MonoBehaviour
    {
        private static readonly Vector2[] Directions =
        {
            new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f),
            new Vector2(0.7071f, 0.7071f), new Vector2(-0.7071f, 0.7071f), new Vector2(0.7071f, -0.7071f), new Vector2(-0.7071f, -0.7071f),
        };

        [SerializeField] private Material silhouetteMaterial;
        [SerializeField] private Color color = new Color(1f, 0.93f, 0.45f, 1f);
        [Tooltip("Rim thickness in world units (1 unit = one tile width).")]
        [SerializeField] private float thickness = 0.06f;
        [SerializeField] private float pulseSpeed = 3f;
        [Range(0f, 1f)] [SerializeField] private float pulseAmount = 0.35f;

        private readonly List<SpriteRenderer> _rim = new List<SpriteRenderer>();
        private readonly List<GameObject> _parts = new List<GameObject>();
        private BuildingView _target;

        public BuildingView Target => _target != null ? _target : null;
        public bool IsShowing => _target != null && _parts.Count > 0;
        public int RimRendererCount => _rim.Count;

        public void Show(BuildingView view)
        {
            if (view == null) { Hide(); return; }
            if (_target == view && IsShowing) return;
            Hide();
            _target = view;

            // Building sprite plus children on the same layer (the crop on a field). Not badges or icons.
            var sources = new List<SpriteRenderer>();
            foreach (var r in view.GetComponentsInChildren<SpriteRenderer>())
            {
                if (r.sprite == null || !r.enabled) continue;
                if (r.sortingLayerID != view.SpriteRenderer.sortingLayerID) continue;
                sources.Add(r);
            }

            foreach (var source in sources)
            {
                var mask = new GameObject("OutlineMask").AddComponent<SpriteMask>();
                mask.transform.SetParent(source.transform, false);
                mask.sprite = source.sprite;
                mask.alphaCutoff = 0.1f;
                _parts.Add(mask.gameObject);

                foreach (var dir in Directions)
                {
                    var rim = new GameObject("OutlineRim").AddComponent<SpriteRenderer>();
                    rim.transform.SetParent(source.transform, false);
                    // Offset in world units regardless of the parent's scale.
                    var lossy = source.transform.lossyScale;
                    rim.transform.localPosition = new Vector3(dir.x * thickness / Mathf.Max(0.0001f, lossy.x),
                        dir.y * thickness / Mathf.Max(0.0001f, lossy.y), 0f);
                    rim.sprite = source.sprite;
                    rim.flipX = source.flipX;
                    rim.flipY = source.flipY;
                    if (silhouetteMaterial != null) rim.sharedMaterial = silhouetteMaterial;
                    rim.sortingLayerName = SortingLayers.Effects;
                    rim.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
                    rim.color = color;
                    _rim.Add(rim);
                    _parts.Add(rim.gameObject);
                }
            }

            ApplyColor(1f);
        }

        public void Hide()
        {
            foreach (var go in _parts)
            {
                if (go == null) continue;
                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
            }

            _parts.Clear();
            _rim.Clear();
            _target = null;
        }

        private void Update()
        {
            if (_target == null)
            {
                // The building was removed while selected (its children went with it).
                if (_parts.Count > 0) Hide();
                return;
            }

            float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed);
            ApplyColor(1f - pulseAmount * wave);
        }

        private void ApplyColor(float alpha)
        {
            var c = new Color(color.r, color.g, color.b, color.a * alpha);
            foreach (var r in _rim)
                if (r != null) r.color = c;
        }

        private void OnDestroy() => Hide();
    }
}
