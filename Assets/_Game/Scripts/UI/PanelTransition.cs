using UnityEngine;

namespace TownBuilder
{
    /// <summary>
    /// Plays a short open animation whenever the panel it sits on is shown (SetActive(true)): the panel fades in, and
    /// either its "Window" child pops in (dialogs, full-screen panels) or the panel itself slides up (bottom bars and panels).
    /// Closing stays instant. No code changes needed in the panels; the scene builder adds this to every panel root.
    /// </summary>
    public sealed class PanelTransition : MonoBehaviour
    {
        [SerializeField] private float duration = 0.2f;
        [SerializeField] private float slideDistance = 60f;

        private CanvasGroup _group;
        private RectTransform _rect;
        private RectTransform _window;
        private Vector2 _basePosition;
        private float _age = -1f;

        public bool IsPlaying => _age >= 0f;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _basePosition = _rect.anchoredPosition;
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            var w = transform.Find("Window");
            _window = w != null ? (RectTransform)w : null;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            _age = 0f;
            Apply(0f);
        }

        private void OnDisable()
        {
            if (_age >= 0f) Apply(1f);
            _age = -1f;
        }

        private void Update()
        {
            if (_age < 0f) return;
            _age += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_age / duration);
            Apply(k);
            if (k >= 1f) _age = -1f;
        }

        private void Apply(float k)
        {
            float eased = 1f - (1f - k) * (1f - k);
            _group.alpha = eased;
            if (_window != null)
            {
                // Pop: 0.9 → 1.03 → 1.
                float s = k < 0.7f ? Mathf.Lerp(0.9f, 1.03f, k / 0.7f) : Mathf.Lerp(1.03f, 1f, (k - 0.7f) / 0.3f);
                _window.localScale = Vector3.one * s;
            }
            else
            {
                _rect.anchoredPosition = _basePosition + new Vector2(0f, -slideDistance * (1f - eased));
            }
        }
    }
}
