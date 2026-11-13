using System;
using UnityEngine;

namespace TownBuilder
{
    /// <summary>
    /// Plays a short open animation whenever the panel it sits on is shown (SetActive(true)): the panel fades in, and
    /// either its "Window" child pops in (dialogs, full-screen panels) or the panel itself slides up (bottom bars and panels).
    /// No code changes needed in the panels; the scene builder adds this to every panel root.
    /// v0.41.0: windows grow from 92% with a small overshoot over 0.3 s; <see cref="Close"/> plays it backwards before
    /// hiding (panels that don't call it still close at once); "Reduce motion" makes both instant.
    /// </summary>
    public sealed class PanelTransition : MonoBehaviour
    {
        [SerializeField] private float duration = 0.2f;
        [SerializeField] private float windowDuration = 0.3f;
        [SerializeField] private float closeDuration = 0.18f;
        [SerializeField] private float slideDistance = 60f;

        /// <summary>Window scale at the start of opening (and the end of closing).</summary>
        public const float WindowStartScale = 0.92f;

        private CanvasGroup _group;
        private RectTransform _rect;
        private RectTransform _window;
        private Vector2 _basePosition;
        private float _age = -1f;
        private bool _closing;
        private Action _onClosed;
        private bool _ready;

        public bool IsPlaying => _age >= 0f;
        public bool IsClosing => _closing;

        private float Length => _closing ? closeDuration : _window != null ? windowDuration : duration;

        private void Awake() => Setup();

        private void Setup()
        {
            if (_ready) return;
            _ready = true;
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
            Setup();
            _closing = false;
            _onClosed = null;
            _group.interactable = true;
            _group.blocksRaycasts = true;
            if (MotionPreferences.ReduceMotion) { _age = -1f; Apply(1f); return; }
            _age = 0f;
            Apply(0f);
        }

        private void OnDisable()
        {
            _closing = false;
            _onClosed = null;
            if (_group != null) { _group.interactable = true; _group.blocksRaycasts = true; }
            if (_ready) Apply(1f);
            _age = -1f;
        }

        /// <summary>
        /// Plays the opening backwards (fade out, window shrinks), then deactivates the panel and calls
        /// <paramref name="onClosed"/>. Input is blocked meanwhile. Instant with Reduce motion or when inactive.
        /// </summary>
        public void Close(Action onClosed = null)
        {
            Setup();
            if (!gameObject.activeInHierarchy || MotionPreferences.ReduceMotion || !Application.isPlaying)
            {
                gameObject.SetActive(false);
                onClosed?.Invoke();
                return;
            }

            if (_closing) { _onClosed += onClosed; return; }
            _closing = true;
            _onClosed = onClosed;
            _group.interactable = false;
            _group.blocksRaycasts = false;
            _age = 0f;
        }

        private void Update()
        {
            if (_age < 0f) return;
            _age += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_age / Length);
            Apply(_closing ? 1f - k : k);
            if (k < 1f) return;
            _age = -1f;
            if (!_closing) return;
            var done = _onClosed;
            gameObject.SetActive(false); // OnDisable resets the closing state
            done?.Invoke();
        }

        private void Apply(float k)
        {
            float eased = 1f - (1f - k) * (1f - k);
            _group.alpha = eased;
            if (_window != null)
            {
                _window.localScale = Vector3.one * WindowScale(k);
            }
            else
            {
                _rect.anchoredPosition = _basePosition + new Vector2(0f, -slideDistance * (1f - eased));
            }
        }

        /// <summary>Window scale at progress <paramref name="k"/> (0-1): 0.92 → a little over 1 → 1 (ease-out back).</summary>
        public static float WindowScale(float k)
        {
            k = Mathf.Clamp01(k);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float t = k - 1f;
            float back = 1f + c3 * t * t * t + c1 * t * t; // 0 → ~1.1 → 1
            return Mathf.LerpUnclamped(WindowStartScale, 1f, back);
        }
    }
}
