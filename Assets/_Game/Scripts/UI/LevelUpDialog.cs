using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TownBuilder
{
    /// <summary>"Level 3!" celebration with rewards and newly unlocked content. Several level-ups in a row are queued.</summary>
    public sealed class LevelUpDialog : MonoBehaviour
    {
        [SerializeField] private GameObject dialogRoot;
        [SerializeField] private Text titleText;
        [SerializeField] private Text rewardsText;
        [SerializeField] private Text unlocksText;
        [SerializeField] private Button okButton;

        private readonly Queue<LevelUpInfo> _pending = new Queue<LevelUpInfo>();

        public bool IsOpen => dialogRoot.activeSelf;

        private void Awake()
        {
            okButton.onClick.AddListener(ShowNextOrClose);
            dialogRoot.SetActive(false);
        }

        public void Enqueue(LevelUpInfo info)
        {
            _pending.Enqueue(info);
            if (!IsOpen) ShowNextOrClose();
        }

        private void ShowNextOrClose()
        {
            if (_pending.Count == 0)
            {
                dialogRoot.SetActive(false);
                return;
            }

            var info = _pending.Dequeue();
            titleText.text = $"Level {info.Level}!";
            var rewards = new List<string>();
            if (info.Coins > 0) rewards.Add($"+{info.Coins:N0} coins");
            if (info.Gems > 0) rewards.Add($"+{info.Gems} gems");
            rewardsText.text = rewards.Count > 0 ? string.Join("    ", rewards) : "";
            unlocksText.text = info.Unlocks.Count > 0 ? "New: " + string.Join(", ", info.Unlocks) : "Keep growing your town!";
            dialogRoot.SetActive(true);
        }
    }
}
