using System;
using System.Collections.Generic;
using System.Linq;
using TownBuilder.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace TownBuilder
{
    /// <summary>
    /// The Build shop: one tab per building category that has something for sale (Houses, Town, Factories, Farm,
    /// Decorations), each with a small picture of one of its buildings. Tabs and item cards are cloned from hidden
    /// templates, so new content needs no scene changes.
    /// </summary>
    public sealed class BuildingShopPanel : MonoBehaviour
    {
        /// <summary>Tab order. Categories with nothing in the shop get no tab.</summary>
        public static readonly BuildingCategory[] TabOrder =
        {
            BuildingCategory.Residential, BuildingCategory.Community, BuildingCategory.Production,
            BuildingCategory.Farming, BuildingCategory.Decoration,
        };

        public static string TabLabel(BuildingCategory category) => category switch
        {
            BuildingCategory.Residential => "Houses",
            BuildingCategory.Community => "Town",
            BuildingCategory.Production => "Factories",
            BuildingCategory.Farming => "Farm",
            _ => "Decorations",
        };

        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Transform itemsParent;
        [SerializeField] private ShopItemView itemTemplate;
        [SerializeField] private Button closeButton;
        [SerializeField] private Transform tabsParent;
        [SerializeField] private ShopTabView tabTemplate;
        [SerializeField] private Color activeTabColor = new Color(0.3f, 0.66f, 0.3f);
        [SerializeField] private Color idleTabColor = new Color(0.62f, 0.62f, 0.6f);
        [SerializeField] private ScrollRect scroll;

        private readonly List<ShopItemView> _items = new List<ShopItemView>();
        private readonly List<ShopTabView> _tabs = new List<ShopTabView>();
        private Wallet _wallet;
        private PlayerProgress _progress;
        private Action<BuildingDefinition> _onSelected;
        private Func<BuildingDefinition, bool> _canBuildMore;

        public bool IsOpen => panelRoot.activeSelf;
        public BuildingCategory CurrentCategory { get; private set; }
        public bool ShowingDecorations => CurrentCategory == BuildingCategory.Decoration;
        public IReadOnlyList<BuildingCategory> Categories => _tabs.Select(t => t.Category).ToList();

        /// <summary>Cards visible in the current tab (tests).</summary>
        public int VisibleItems => _items.Count(i => i.gameObject.activeSelf);

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

            BuildTabs(content);
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
            if (_tabs.Count > 0) ShowCategory(_tabs[0].Category);
            Close();
        }

        /// <summary>Creates one tab per category with shop items (also used by the editor preview).</summary>
        public void BuildTabs(GameContent content)
        {
            tabTemplate.gameObject.SetActive(false);
            foreach (var tab in _tabs)
                if (tab != null) Destroy(tab.gameObject);
            _tabs.Clear();

            foreach (var category in TabOrder)
            {
                var first = content.Buildings.FirstOrDefault(b => b != null && b.ShowInShop && b.Category == category);
                if (first == null) continue;
                var tab = Instantiate(tabTemplate, tabsParent);
                tab.name = $"Tab_{category}";
                tab.gameObject.SetActive(true);
                tab.Bind(category, TabLabel(category), first.Sprite, ShowCategory);
                _tabs.Add(tab);
            }
        }

        /// <summary>Highlights a tab without touching item cards (editor preview).</summary>
        public void HighlightTab(BuildingCategory category)
        {
            CurrentCategory = category;
            foreach (var tab in _tabs) tab.SetSelected(tab.Category == category, activeTabColor, idleTabColor);
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

        /// <summary>Shows the cards of one category.</summary>
        public void ShowCategory(BuildingCategory category)
        {
            HighlightTab(category);
            foreach (var item in _items) item.gameObject.SetActive(item.Definition.Category == category);
            if (scroll != null) scroll.horizontalNormalizedPosition = 0f;
        }

        private void OnItemClicked(BuildingDefinition definition)
        {
            Close();
            _onSelected?.Invoke(definition);
        }
    }
}
