using System;
using UnityEngine;
using UnityEngine.UI;

namespace TownBuilder
{
    public sealed class ShopItemView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private Text nameText;
        [SerializeField] private Text priceText;

        public BuildingDefinition Definition { get; private set; }

        public void Bind(BuildingDefinition definition, Action<BuildingDefinition> onClicked)
        {
            Definition = definition;
            icon.sprite = definition.Sprite;
            icon.preserveAspect = true;
            nameText.text = definition.DisplayName;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClicked?.Invoke(Definition));
        }

        public void Refresh(bool unlocked, bool affordable, bool limitReached)
        {
            button.interactable = unlocked && affordable && !limitReached;
            if (limitReached) priceText.text = Definition.MaxCount == 1 ? "Built" : $"Max {Definition.MaxCount}";
            else priceText.text = unlocked ? $"{Definition.CoinCost:N0} coins" : $"Level {Definition.UnlockLevel}";
            priceText.color = unlocked && !affordable && !limitReached ? new Color(0.85f, 0.2f, 0.2f) : new Color(0.2f, 0.2f, 0.2f);
            icon.color = unlocked && !limitReached ? Color.white : new Color(0.3f, 0.3f, 0.3f, 0.8f);
        }
    }
}
