using System;
using TownBuilder.Domain;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TownBuilder
{
    /// <summary>One order: up to 3 item rows ("Bread 1/2" in green or red), reward, Deliver and Skip.</summary>
    public sealed class OrderCardView : MonoBehaviour
    {
        [SerializeField] private Image[] rowIcons = new Image[0];
        [SerializeField] private TMP_Text[] rowTexts = new TMP_Text[0];
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private TMP_Text waitingText;
        [SerializeField] private Button deliverButton;
        [SerializeField] private Button skipButton;

        private static readonly Color Have = new Color(0.15f, 0.55f, 0.2f);
        private static readonly Color Need = new Color(0.8f, 0.2f, 0.2f);

        public void Initialize(Action onDeliver, Action onSkip)
        {
            deliverButton.onClick.AddListener(() => onDeliver?.Invoke());
            skipButton.onClick.AddListener(() => onSkip?.Invoke());
        }

        public void Show(Order order, long nowUnixMs, bool canDeliver, GameContent content, Inventory inventory)
        {
            bool available = order != null && order.IsAvailable(nowUnixMs);
            waitingText.gameObject.SetActive(!available);
            waitingText.text = order == null
                ? "No order"
                : $"New order in\n{FieldPanel.FormatDuration(order.AvailableAtUnixMs - nowUnixMs)}";

            for (int i = 0; i < rowTexts.Length; i++)
            {
                bool used = available && i < order.Lines.Count;
                rowIcons[i].transform.parent.gameObject.SetActive(used);
                if (!used) continue;

                var line = order.Lines[i];
                content.TryGetItem(line.ItemId, out var item);
                int have = inventory != null ? inventory.Count(line.ItemId) : 0;
                rowIcons[i].sprite = item != null ? item.Icon : null;
                rowIcons[i].enabled = rowIcons[i].sprite != null;
                rowIcons[i].preserveAspect = true;
                rowTexts[i].text = $"{(item != null ? item.DisplayName : line.ItemId)}   {Math.Min(have, line.Amount)}/{line.Amount}";
                rowTexts[i].color = have >= line.Amount ? Have : Need;
            }

            rewardText.gameObject.SetActive(available);
            if (available) rewardText.text = $"+{order.RewardCoins:N0} coins   +{order.RewardXp} XP";
            deliverButton.gameObject.SetActive(available);
            deliverButton.interactable = canDeliver;
            skipButton.gameObject.SetActive(available);
        }
    }
}
