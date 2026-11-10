using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TownBuilder
{
    /// <summary>One achievement row: name, goal, tier, progress bar + "120/300", gem reward, and a Claim button once reached.</summary>
    public sealed class AchievementRowView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text goalText;
        [SerializeField] private TMP_Text tierText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private Image progressFill;
        [SerializeField] private Button claimButton;

        public Button ClaimButton => claimButton;

        public void Show(string title, string goal, string tier, string progress, float fill, string reward, bool canClaim, Color color)
        {
            gameObject.SetActive(true);
            background.color = color;
            nameText.text = title;
            goalText.text = goal;
            tierText.text = tier;
            progressText.text = progress;
            progressFill.fillAmount = fill;
            rewardText.text = reward;
            rewardText.gameObject.SetActive(reward.Length > 0);
            claimButton.gameObject.SetActive(canClaim);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
