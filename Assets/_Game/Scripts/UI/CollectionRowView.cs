using System;
using TMPro;
using TownBuilder.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace TownBuilder
{
    /// <summary>One collection row: name, where it's found, five item slots (icon + count), reward, Complete.</summary>
    public sealed class CollectionRowView : MonoBehaviour
    {
        private static readonly Color Missing = new Color(0.15f, 0.13f, 0.12f, 0.35f);

        [SerializeField] private Image background;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text sourceText;
        [SerializeField] private Image[] slotIcons = new Image[0];
        [SerializeField] private TMP_Text[] slotCounts = new TMP_Text[0];
        [SerializeField] private TMP_Text[] slotNames = new TMP_Text[0];
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private Button completeButton;

        public void Show(CollectionDefinition def, CollectionBook book, Action onComplete)
        {
            gameObject.SetActive(true);
            bool ready = book.CanComplete(def.Id);
            background.color = ready ? new Color(0.86f, 0.96f, 0.82f) : Color.white;
            int done = book.Completions(def.Id);
            nameText.text = done > 0 ? $"{def.DisplayName}  (completed {done}x)" : def.DisplayName;
            sourceText.text = $"{def.SourceText}  ·  {book.DistinctOwned(def.Id)}/{def.Items.Count} found";

            for (int i = 0; i < slotIcons.Length; i++)
            {
                bool has = i < def.Items.Count;
                slotIcons[i].transform.parent.gameObject.SetActive(has);
                if (!has) continue;
                var item = def.Items[i];
                int count = book.Count(item.id);
                slotIcons[i].sprite = item.icon;
                slotIcons[i].color = count > 0 ? Color.white : Missing;
                slotCounts[i].text = count > 1 ? $"x{count}" : "";
                slotNames[i].text = count > 0 ? item.displayName : "?";
            }

            rewardText.text = $"Reward: {def.RewardText()}";
            completeButton.gameObject.SetActive(true);
            completeButton.interactable = ready;
            completeButton.onClick.RemoveAllListeners();
            if (onComplete != null) completeButton.onClick.AddListener(() => onComplete());
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
