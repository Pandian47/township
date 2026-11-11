using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TownBuilder
{
    /// <summary>One prize of a weekend event: goal, progress bar, prize picture + text, Claim button / "Claimed".</summary>
    public sealed class EventPrizeRowView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text stepText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private Image progressFill;
        [SerializeField] private Image rewardIcon;
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private Button claimButton;
        [SerializeField] private TMP_Text doneText;

        public Button ClaimButton => claimButton;

        public void Show(string step, int points, int goal, Sprite icon, string reward, bool canClaim, bool claimed, Color colour)
        {
            gameObject.SetActive(true);
            background.color = colour;
            stepText.text = step;
            progressText.text = $"{Mathf.Min(points, goal):N0}/{goal:N0}";
            progressFill.fillAmount = goal > 0 ? Mathf.Clamp01((float)points / goal) : 0f;
            rewardIcon.sprite = icon;
            rewardIcon.enabled = icon != null;
            rewardText.text = reward;
            claimButton.gameObject.SetActive(canClaim);
            doneText.gameObject.SetActive(claimed);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
