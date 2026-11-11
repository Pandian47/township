using System;
using TMPro;
using TownBuilder.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace TownBuilder
{
    /// <summary>
    /// Weekend event: a button on the left of the town (name, time left, badge when a prize is ready; only during an
    /// event) and the event window (what earns points, time left, three prizes).
    /// </summary>
    public sealed class EventPanel : MonoBehaviour
    {
        private static readonly Color Normal = Color.white;
        private static readonly Color Ready = new Color(0.86f, 0.96f, 0.82f);
        private static readonly Color Done = new Color(1f, 0.92f, 0.62f);

        [Header("Button (town)")]
        [SerializeField] private GameObject buttonRoot;
        [SerializeField] private Button openButton;
        [SerializeField] private TMP_Text buttonTitle;
        [SerializeField] private TMP_Text buttonTimer;
        [SerializeField] private GameObject badge;

        [Header("Window")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text pointsText;
        [SerializeField] private EventPrizeRowView[] rows = new EventPrizeRowView[0];
        [SerializeField] private Sprite coinSprite;
        [SerializeField] private Sprite gemSprite;

        private WeekendEventService _service;
        private float _nextTick;

        public bool IsOpen => panelRoot.activeSelf;
        public bool ButtonVisible => buttonRoot.activeSelf;

        public void Initialize(WeekendEventService service)
        {
            Unbind();
            _service = service;
            openButton.onClick.AddListener(Open);
            closeButton.onClick.AddListener(() => panelRoot.SetActive(false));
            for (int i = 0; i < rows.Length; i++)
            {
                int index = i;
                rows[i].ClaimButton.onClick.AddListener(() => _service.Claim(index));
            }

            _service.Modified += Refresh;
            _service.ActiveChanged += Refresh;
            panelRoot.SetActive(false);
            Refresh();
        }

        public void Open()
        {
            if (_service == null || !_service.IsActive) return;
            panelRoot.SetActive(true);
            Refresh();
        }

        private void Update()
        {
            if (_service == null || Time.unscaledTime < _nextTick) return;
            _nextTick = Time.unscaledTime + 1f;
            _service.Tick(); // starts / ends the event at midnight
            RefreshTimers();
        }

        private void Refresh()
        {
            if (_service == null) return;
            bool active = _service.IsActive;
            buttonRoot.SetActive(active);
            if (!active)
            {
                panelRoot.SetActive(false);
                return;
            }

            Render(_service.Current, _service.Progress, _service.ClaimableCount > 0);
            RefreshTimers();
        }

        /// <summary>Editor preview: shows the button and window for an event without a running service.</summary>
        public void PreviewRender(EventDefinition ev, EventProgress progress, string timeLeft)
        {
            buttonRoot.SetActive(true);
            panelRoot.SetActive(true);
            Render(ev, progress, progress.CanClaim(progress.Claimed));
            buttonTimer.text = timeLeft;
            timerText.text = $"Ends in {timeLeft}";
        }

        private void Render(EventDefinition ev, EventProgress progress, bool claimable)
        {
            buttonTitle.text = ev.DisplayName;
            badge.SetActive(claimable);
            titleText.text = ev.DisplayName;
            descriptionText.text = ev.Description;
            pointsText.text = $"{ev.PointsLabel} this weekend: {progress.Points:N0}";

            for (int i = 0; i < rows.Length; i++)
            {
                if (i >= ev.Milestones.Count)
                {
                    rows[i].Hide();
                    continue;
                }

                var m = ev.Milestones[i];
                bool claimed = i < progress.Claimed;
                bool canClaim = progress.CanClaim(i);
                Sprite icon = m.decoration != null ? m.decoration.Sprite : m.gems > 0 ? gemSprite : coinSprite;
                rows[i].Show($"Prize {i + 1}", progress.Points, m.goal, icon, m.RewardText(), canClaim, claimed,
                    claimed ? Done : canClaim ? Ready : Normal);
            }
        }

        private void RefreshTimers()
        {
            if (_service == null || !_service.IsActive) return;
            string left = Format(_service.RemainingMs);
            buttonTimer.text = left;
            timerText.text = $"Ends in {left}";
        }

        public static string Format(long ms)
        {
            var t = TimeSpan.FromMilliseconds(Math.Max(0, ms));
            if (t.TotalDays >= 1) return $"{(int)t.TotalDays}d {t.Hours}h";
            if (t.TotalHours >= 1) return $"{(int)t.TotalHours}h {t.Minutes:D2}m";
            return $"{t.Minutes}m {t.Seconds:D2}s";
        }

        private void Unbind()
        {
            if (_service == null) return;
            _service.Modified -= Refresh;
            _service.ActiveChanged -= Refresh;
        }

        private void OnDestroy() => Unbind();
    }
}
