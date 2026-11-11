using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TownBuilder
{
    /// <summary>
    /// One big building card in a Build category window: name, "built / max", what it gives, a large picture, and a
    /// green price button (or "Level N" / "Built" / "Max" when it can't be bought).
    /// </summary>
    public sealed class ShopItemView : MonoBehaviour
    {
        private static readonly Color Brown = new Color(0.40f, 0.24f, 0.10f);

        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text priceText;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private TMP_Text bonusText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Image background;

        public BuildingDefinition Definition { get; private set; }
        public Button BuyButton => button;

        public void Bind(BuildingDefinition definition, Action<BuildingDefinition> onClicked)
        {
            Definition = definition;
            icon.sprite = definition.Sprite;
            icon.preserveAspect = true;
            nameText.text = definition.DisplayName;
            if (bonusText != null) bonusText.text = Bonus(definition);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClicked?.Invoke(Definition));
        }

        /// <param name="owned">How many the player already has (shown as "owned / max" for limited buildings).</param>
        public void Refresh(bool unlocked, bool affordable, bool limitReached, int owned = 0)
        {
            bool canBuy = unlocked && !limitReached;
            button.gameObject.SetActive(canBuy);
            button.interactable = canBuy && affordable;
            priceText.text = Definition.CoinCost > 0 ? Definition.CoinCost.ToString("N0") : "Free";
            priceText.color = affordable ? Color.white : new Color(1f, 0.75f, 0.7f);

            if (statusText != null)
            {
                statusText.gameObject.SetActive(!canBuy);
                statusText.text = limitReached ? (Definition.MaxCount == 1 ? "Built" : "Max")
                    : $"Level {Definition.UnlockLevel}\nrequired";
            }

            if (countText != null)
                countText.text = Definition.MaxCount > 0 ? $"{owned}/{Definition.MaxCount}" : owned > 0 ? $"Owned {owned}" : "";

            icon.color = canBuy ? Color.white : new Color(1f, 1f, 1f, 0.55f);
            if (background != null) background.color = canBuy ? new Color(1f, 0.985f, 0.94f) : new Color(0.93f, 0.89f, 0.80f);
            nameText.color = Brown;
        }

        /// <summary>What the building gives, in a few words.</summary>
        public static string Bonus(BuildingDefinition d)
        {
            if (d.IsLandscapeTool)
                return d.LandscapeTool == TownBuilder.Domain.LandscapeTool.Roads ? "Draw roads"
                    : d.LandscapeTool == TownBuilder.Domain.LandscapeTool.Paving ? "Pave the ground" : "Paint the ground";
            if (d.Happiness > 0) return $"+{d.Happiness} happy";
            if (d.StorageCapacityBonus > 0) return $"+{d.StorageCapacityBonus} barn";
            if (d.IsFruitTree)
            {
                long min = d.Fruit.GrowDurationMs / 60000;
                return $"{d.Fruit.DisplayName} every {(min >= 60 && min % 60 == 0 ? $"{min / 60} h" : $"{min} min")}";
            }
            if (d.IsFactory) return d.Recipes.Count == 1 ? d.Recipes[0].DisplayName : $"{d.Recipes.Count(r => r != null)} products";
            if (d.IsFarmPlot) return "Grows crops";
            if (d.Category == BuildingCategory.Residential) return "Earns coins";
            return "";
        }
    }
}
