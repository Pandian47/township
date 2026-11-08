using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TownBuilder
{
    public sealed class BarnItemView : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text priceText;
        [SerializeField] private Button sellOneButton;
        [SerializeField] private Button sellAllButton;
        [SerializeField] private TMP_Text sellAllLabel;

        private string _itemId;
        private Action<string> _sellOne;
        private Action<string> _sellAll;

        private void Awake()
        {
            sellOneButton.onClick.AddListener(() => _sellOne?.Invoke(_itemId));
            sellAllButton.onClick.AddListener(() => _sellAll?.Invoke(_itemId));
        }

        public void Show(string itemId, ItemDefinition item, int amount, Action<string> sellOne, Action<string> sellAll)
        {
            _itemId = itemId;
            _sellOne = sellOne;
            _sellAll = sellAll;

            icon.sprite = item != null ? item.Icon : null;
            icon.enabled = icon.sprite != null;
            icon.preserveAspect = true;
            nameText.text = item != null ? item.DisplayName : itemId;

            bool sellable = item != null && item.CanBeSold;
            priceText.text = sellable ? $"x{amount}  ·  {item.SellPrice} coins each" : $"x{amount}  ·  can't be sold";
            sellOneButton.interactable = sellable;
            sellAllButton.interactable = sellable;
            sellAllLabel.text = sellable ? $"Sell all\n+{(long)item.SellPrice * amount:N0}" : "Sell all";
        }
    }
}
