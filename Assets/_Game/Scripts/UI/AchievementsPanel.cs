using TMPro;
using TownBuilder.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace TownBuilder
{
    /// <summary>
    /// "Goals" screen: one row per achievement with its current tier, progress and gem reward, plus the Goals button
    /// in the bottom bar with a badge counting tiers that can be claimed.
    /// </summary>
    public sealed class AchievementsPanel : MonoBehaviour
    {
        private static readonly Color Normal = Color.white;
        private static readonly Color Ready = new Color(0.86f, 0.96f, 0.82f);
        private static readonly Color Finished = new Color(1f, 0.88f, 0.5f);

        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private GameObject badge;
        [SerializeField] private TMP_Text badgeText;
        [SerializeField] private AchievementRowView[] rows = new AchievementRowView[0];

        private AchievementService _service;

        public bool IsOpen => panelRoot.activeSelf;

        public void Initialize(AchievementService service)
        {
            Unbind();
            _service = service;
            openButton.onClick.AddListener(Open);
            closeButton.onClick.AddListener(() => panelRoot.SetActive(false));
            _service.Modified += Refresh;
            panelRoot.SetActive(false);
            Refresh();
        }

        public void Open()
        {
            panelRoot.SetActive(true);
            Refresh();
        }

        private void Refresh()
        {
            if (_service == null) return;
            Render(_service.Book, _service.Content);
        }

        /// <summary>Draws the rows and badge from a book (also used by the editor preview without a service).</summary>
        public void Render(AchievementBook book, GameContent content)
        {
            int claimable = book.ClaimableCount;
            if (badge != null) badge.SetActive(claimable > 0);
            if (badgeText != null) badgeText.text = claimable.ToString();

            int r = 0;
            foreach (var spec in book.Specs)
            {
                if (r >= rows.Length) break;
                if (!content.TryGetAchievement(spec.Id, out var def)) continue;
                var row = rows[r++];
                string id = spec.Id;
                long count = book.Count(spec.Kind);
                int claimed = book.ClaimedTiers(id);
                bool finished = book.IsFinished(id);
                bool canClaim = book.CanClaim(id);
                int shownTier = finished ? spec.TierCount - 1 : claimed;
                int goal = spec.Goals[shownTier];

                row.Show(def.DisplayName, def.Describe(goal), $"Tier {shownTier + 1} of {spec.TierCount}",
                    finished ? "Complete!" : $"{System.Math.Min(count, goal):N0}/{goal:N0}",
                    finished ? 1f : Mathf.Clamp01((float)count / goal),
                    finished ? "" : $"+{def.GemsForTier(claimed)} gems",
                    canClaim, finished ? Finished : canClaim ? Ready : Normal);
                row.ClaimButton.onClick.RemoveAllListeners();
                if (_service != null) row.ClaimButton.onClick.AddListener(() => _service.Claim(id));
            }

            for (; r < rows.Length; r++) rows[r].Hide();
        }

        private void Unbind()
        {
            if (_service != null) _service.Modified -= Refresh;
        }

        private void OnDestroy() => Unbind();
    }
}
