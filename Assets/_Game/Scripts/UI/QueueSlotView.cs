using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TownBuilder
{
    /// <summary>
    /// One production slot (a basket on the factory window's shelf): empty, waiting, in progress (with a ring that fills
    /// and the time left) or ready (soft golden glow, one small bounce, "Ready!" tag).
    /// </summary>
    public sealed class QueueSlotView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text statusText;
        [Tooltip("Filled image: the progress ring around the product being made.")]
        [SerializeField] private Image progressFill;
        [Tooltip("Soft glow behind a finished product (optional).")]
        [SerializeField] private Image glow;

        public enum State { Empty, Waiting, Active, Ready }

        private static readonly Color EmptyColor = Color.white;
        private static readonly Color BusyColor = Color.white;
        private static readonly Color ReadyColor = new Color(1f, 0.97f, 0.82f);

        private const float PopSeconds = 0.32f;
        private float _popAge = -1f;
        private float _popStrength;

        public State Current { get; private set; } = State.Empty;
        public Image Icon => icon;

        /// <summary>Forget the last state (another factory is shown), so its finished products don't bounce.</summary>
        public void ResetState() => Current = State.Empty;

        public void ShowEmpty() => Set(State.Empty, null, EmptyColor, "", 0f);

        public void ShowWaiting(Sprite sprite) => Set(State.Waiting, sprite, BusyColor, "Waiting", 0f);

        public void ShowActive(Sprite sprite, float progress, string timeLeft) => Set(State.Active, sprite, BusyColor, timeLeft, progress);

        public void ShowReady(Sprite sprite) => Set(State.Ready, sprite, ReadyColor, "Ready!", 1f);

        /// <summary>A quick grow-and-settle (a product just landed here). Skipped with Reduce motion.</summary>
        public void Pop(float strength = 0.15f)
        {
            if (MotionPreferences.ReduceMotion) return;
            _popStrength = strength;
            _popAge = 0f;
        }

        private void Set(State state, Sprite sprite, Color bg, string status, float progress)
        {
            bool becameReady = state == State.Ready && Current != State.Ready && Current != State.Empty;
            Current = state;
            background.color = bg;
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            icon.preserveAspect = true;
            icon.color = state == State.Waiting ? new Color(1f, 1f, 1f, 0.75f) : Color.white;
            statusText.text = status;
            // The ring (its track and its fill) only shows around the product being made.
            progressFill.transform.parent.gameObject.SetActive(state == State.Active);
            progressFill.fillAmount = progress;
            if (glow != null) glow.gameObject.SetActive(state == State.Ready);
            if (becameReady) Pop(0.2f);
        }

        private void Update()
        {
            if (glow != null && glow.gameObject.activeSelf)
            {
                float a = MotionPreferences.ReduceMotion ? 0.8f : 0.65f + 0.25f * Mathf.Sin(Time.unscaledTime * 3f);
                glow.color = new Color(1f, 0.85f, 0.3f, a);
            }

            if (_popAge < 0f) return;
            _popAge += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_popAge / PopSeconds);
            float s = 1f + _popStrength * Mathf.Sin(k * Mathf.PI) * (1f - k * 0.5f);
            transform.localScale = Vector3.one * s;
            if (k >= 1f)
            {
                transform.localScale = Vector3.one;
                _popAge = -1f;
            }
        }

        private void OnDisable()
        {
            transform.localScale = Vector3.one;
            _popAge = -1f;
        }
    }
}
