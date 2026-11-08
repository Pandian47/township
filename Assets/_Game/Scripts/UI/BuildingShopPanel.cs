using System;
using System.Collections.Generic;
using TownBuilder.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace TownBuilder
{
    /// <summary>Lists every building definition from <see cref="GameContent"/>. Items are cloned from a hidden template.</summary>
    public sealed class BuildingShopPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Transform itemsParent;
        [SerializeField] private ShopItemView itemTemplate;
        [SerializeField] private Button closeButton;

        private readonly List<ShopItemView> _items = new List<ShopItemView>();
        private Wallet _wallet;
        private PlayerProgress _progress;
        private Action<BuildingDefinition> _onSelected;
        private Func<BuildingDefinition, bool> _canBuildMore;

        public bool IsOpen => panelRoot.activeSelf;

        public void Initialize(GameContent content, Wallet wallet, PlayerProgress progress,
            Func<BuildingDefinition, bool> canBuildMore, Action<BuildingDefinition> onSelected)
        {
            _wallet = wallet;
            _progress = progress;
            _canBuildMore = canBuildMore;
            _onSelected = onSelected;

            itemTemplate.gameObject.SetActive(false);
            foreach (var item in _items) Destroy(item.gameObject);
            _items.Clear();

            foreach (var definition in content.Buildings)
            {
                if (definition == null || !definition.ShowInShop) continue;
                var item = Instantiate(itemTemplate, itemsParent);
                item.gameObject.SetActive(true);
                item.Bind(definition, OnItemClicked);
                _items.Add(item);
            }

            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
            Close();
        }

        public void Open()
        {
            foreach (var item in _items)
            {
                var d = item.Definition;
                bool limitReached = _canBuildMore != null && !_canBuildMore(d);
                item.Refresh(_progress.IsUnlocked(d.UnlockLevel), _wallet.CanAffordCoins(d.CoinCost), limitReached);
            }

            panelRoot.SetActive(true);
        }

        public void Close() => panelRoot.SetActive(false);

        private void OnItemClicked(BuildingDefinition definition)
        {
            Close();
            _onSelected?.Invoke(definition);
        }
    }
}
