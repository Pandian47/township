using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TownBuilder
{
    /// <summary>
    /// Daily reward calendar: 7 day cards (claimed ones ticked, today's highlighted) and a Claim button. Opens by itself
    /// when the town loads with a reward waiting, and again if the day changes while playing.
    /// </summary>
    public sealed class DailyRewardDialog : MonoBehaviour
    {
        private static readonly Color Claimed = new Color(0.78f, 0.93f, 0.76f);
        private static readonly Color Today = new Color(1f, 0.86f, 0.45f);
        private static readonly Color Future = Color.white;

        [SerializeField] private GameObject dialogRoot;
        [SerializeField] private Image[] cardBackgrounds = new Image[0];
        [SerializeField] private TMP_Text[] dayTexts = new TMP_Text[0];
        [SerializeField] private Image[] icons = new Image[0];
        [SerializeField] private TMP_Text[] amountTexts = new TMP_Text[0];
        [SerializeField] private Button claimButton;
        [SerializeField] private TMP_Text claimLabel;
        [SerializeField] private Button closeButton;
        [SerializeField] private Sprite coinSprite;
        [SerializeField] private Sprite gemSprite;
        [SerializeField] private float checkInterval = 30f;

        private DailyRewardService _service;
        private float _nextCheck;

        public bool IsOpen => dialogRoot.activeSelf;

        public void Initialize(DailyRewardService service)
        {
            _service = service;
            claimButton.onClick.AddListener(() =>
            {
                _service.Claim();
                Refresh();
            });
            closeButton.onClick.AddListener(() => dialogRoot.SetActive(false));
            dialogRoot.SetActive(false);
            if (_service.CanClaim) Open();
            _nextCheck = Time.unscaledTime + checkInterval;
        }

        public void Open()
        {
            if (_service == null || _service.CalendarLength == 0) return;
            dialogRoot.SetActive(true);
            Refresh();
        }

        private void Update()
        {
            if (_service == null || Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + checkInterval;
            if (!IsOpen && _service.CanClaim) Open(); // a new day started while playing
        }

        private void Refresh()
        {
            bool canClaim = _service.CanClaim;
            int today = _service.TodayIndex;
            for (int i = 0; i < cardBackgrounds.Length; i++)
            {
                var entry = _service.EntryAt(i);
                cardBackgrounds[i].gameObject.SetActive(entry != null);
                if (entry == null) continue;

                bool done = i < today || (i == today && !canClaim);
                cardBackgrounds[i].color = done ? Claimed : i == today ? Today : Future;
                dayTexts[i].text = done ? $"Day {i + 1} (done)" : $"Day {i + 1}";

                Sprite icon = entry.material != null && entry.materialAmount > 0 ? entry.material.Icon
                    : entry.gems > 0 ? gemSprite : coinSprite;
                icons[i].sprite = icon;
                icons[i].enabled = icon != null;

                var lines = new System.Collections.Generic.List<string>();
                if (entry.coins > 0) lines.Add($"{entry.coins:N0} coins");
                if (entry.gems > 0) lines.Add(entry.gems == 1 ? "1 gem" : $"{entry.gems} gems");
                if (entry.material != null && entry.materialAmount > 0) lines.Add($"{entry.materialAmount} {entry.material.DisplayName}");
                amountTexts[i].text = string.Join("\n", lines);
            }

            claimButton.interactable = canClaim;
            claimLabel.text = canClaim ? $"Claim day {today + 1}" : "Come back tomorrow!";
        }
    }
}
