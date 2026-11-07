using UnityEngine;
using UnityEngine.UI;

namespace TownBuilder
{
    /// <summary>Short message that appears, stays briefly and fades out ("+2 Wheat", "Barn is full").</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ToastView : MonoBehaviour
    {
        [SerializeField] private Text messageText;
        [SerializeField] private float holdSeconds = 1.6f;
        [SerializeField] private float fadeSeconds = 0.4f;

        private CanvasGroup _group;
        private float _shownAt = float.NegativeInfinity;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }

        public void Show(string message)
        {
            messageText.text = message;
            _shownAt = Time.unscaledTime;
            _group.alpha = 1f;
        }

        private void Update()
        {
            float age = Time.unscaledTime - _shownAt;
            if (age <= holdSeconds) return;
            _group.alpha = Mathf.Clamp01(1f - (age - holdSeconds) / fadeSeconds);
        }
    }
}
