using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TownBuilder
{
    /// <summary>One production slot: empty, waiting, in progress (with timer and bar) or ready.</summary>
    public sealed class QueueSlotView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Image progressFill;

        private static readonly Color EmptyColor = new Color(0.86f, 0.83f, 0.77f);
        private static readonly Color BusyColor = Color.white;
        private static readonly Color ReadyColor = new Color(0.75f, 0.95f, 0.7f);

        public void ShowEmpty() => Set(null, EmptyColor, "Empty", 0f, false);

        public void ShowWaiting(Sprite sprite) => Set(sprite, BusyColor, "Waiting", 0f, false);

        public void ShowActive(Sprite sprite, float progress, string timeLeft) => Set(sprite, BusyColor, timeLeft, progress, true);

        public void ShowReady(Sprite sprite) => Set(sprite, ReadyColor, "Ready!", 1f, false);

        private void Set(Sprite sprite, Color bg, string status, float progress, bool showBar)
        {
            background.color = bg;
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            icon.preserveAspect = true;
            statusText.text = status;
            progressFill.transform.parent.gameObject.SetActive(showBar);
            progressFill.fillAmount = progress;
        }
    }
}
