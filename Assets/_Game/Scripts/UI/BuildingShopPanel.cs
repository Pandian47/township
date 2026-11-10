using System;
using System.Collections.Generic;
using TownBuilder.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace TownBuilder
{
    /// <summary>
    /// The Build shop: two tabs, Buildings and Decorations (BuildingCategory.Decoration). Items are cloned from a hidden
    /// template for every definition that is sold in the shop.
    /// </summary>
    public sealed class BuildingShopPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Transform itemsParent;
        [SerializeField] private ShopItemView itemTemplate;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button buildingsTab;
        [SerializeField] private Button decorationsTab;
        [SerializeField] private Image buildingsTabImage;
        [SerializeField] private Image decorationsTabImage;
        [SerializeField] private Color activeTabColor = new Color(0.3f, 0.66f, 0.3f);
        [SerializeField] private Color idleTabColor = new Color(0.62f, 0.62f, 0.6f);
        [SerializeField] private ScrollRect scroll;

        private readonly List<ShopItemView> _items = new List<ShopItemView>();
        private Wallet _wallet;
        private PlayerProgress _progress;
        private Action<BuildingDefinition> _onSelected;
        private Func<BuildingDefinition, bool> _canBuildMore;

        public bool IsOpen => panelRoot.activeSelf;
        public bool ShowingDecorations { get; private set; }

        /// <summary>Cards visible in the current tab (tests).</summary>
        public int VisibleItems
        {
            get
            {
                int n = 0;
                foreach (var item in _items)
                    if (item.gameObject.activeSelf) n++;
                return n;
            }
        }

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
            if (buildingsTab != null) buildingsTab.onClick.AddListener(() => ShowTab(false));
            if (decorationsTab != null) decorationsTab.onClick.AddListener(() => ShowTab(true));
            ShowTab(false);
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

        /// <summary>Switches between buildings and decorations.</summary>
        public void ShowTab(bool decorations)
        {
            ShowingDecorations = decorations;
            foreach (var item in _items)
                item.gameObject.SetActive((item.Definition.Category == BuildingCategory.Decoration) == decorations);
            if (buildingsTabImage != null) buildingsTabImage.color = decorations ? idleTabColor : activeTabColor;
            if (decorationsTabImage != null) decorationsTabImage.color = decorations ? activeTabColor : idleTabColor;
            if (scroll != null) scroll.horizontalNormalizedPosition = 0f;
        }

        private void OnItemClicked(BuildingDefinition definition)
        {
            Close();
            _onSelected?.Invoke(definition);
        }
    }
}
