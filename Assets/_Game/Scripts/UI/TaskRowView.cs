using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TownBuilder
{
    /// <summary>One task row: goal, progress bar + "4/6", reward, and a Claim button once done.</summary>
    public sealed class TaskRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private Image progressFill;
        [SerializeField] private Button claimButton;

        public Button ClaimButton => claimButton;

        public void Show(string title, int progress, int amount, string reward, bool complete)
        {
            gameObject.SetActive(true);
            titleText.text = title;
            rewardText.text = reward;
            progressText.text = $"{progress}/{amount}";
            progressText.gameObject.SetActive(!complete);
            progressFill.fillAmount = amount > 0 ? Mathf.Clamp01((float)progress / amount) : 0f;
            claimButton.gameObject.SetActive(complete);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
