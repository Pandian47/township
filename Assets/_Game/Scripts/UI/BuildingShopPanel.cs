using System;
using System.Collections.Generic;
using System.Linq;
using TownBuilder.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace TownBuilder
{
    /// <summary>
    /// The Build shop, in two screens:
    ///   1. Category cards (Houses, Town, Factories, Farm, Decorations): big picture cards with a coloured ribbon and a
    ///      badge counting what can be built right now.
    ///   2. The buildings of one category (the card list), with category tabs to switch and Back to return.
    /// Cards, tabs and item cards are cloned from hidden templates, so new content needs no scene changes.
    /// </summary>
    public sealed class BuildingShopPanel : MonoBehaviour
    {
        /// <summary>Category order. Categories with nothing in the shop get no card or tab.</summary>
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

        public static Color CategoryColour(BuildingCategory category) => category switch
        {
            BuildingCategory.Residential => new Color(0.24f, 0.52f, 0.88f),
            BuildingCategory.Community => new Color(0.93f, 0.72f, 0.16f),
            BuildingCategory.Production => new Color(0.95f, 0.52f, 0.16f),
            BuildingCategory.Farming => new Color(0.32f, 0.68f, 0.26f),
            _ => new Color(0.90f, 0.36f, 0.62f),
        };

        /// <summary>The building shown on each category card (falls back to the category's first shop building).</summary>
        private static string CardBuildingId(BuildingCategory category) => category switch
        {
            BuildingCategory.Residential => "cottage",
            BuildingCategory.Community => "barn",
            BuildingCategory.Production => "bakery",
            BuildingCategory.Farming => "apple_tree",
            _ => "fountain",
        };

        [SerializeField] private GameObject panelRoot;
        [Header("Screen 1: category cards")]
        [SerializeField] private GameObject cardsScreen;
        [SerializeField] private Transform cardsParent;
        [SerializeField] private ShopCategoryCardView cardTemplate;
        [SerializeField] private Button cardsCloseButton;
        [Header("Screen 2: buildings of one category")]
        [SerializeField] private GameObject listScreen;
        [SerializeField] private Button backButton;
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
        private readonly List<ShopCategoryCardView> _cards = new List<ShopCategoryCardView>();
        private GameContent _content;
        private Wallet _wallet;
        private PlayerProgress _progress;
        private Action<BuildingDefinition> _onSelected;
        private Func<BuildingDefinition, bool> _canBuildMore;

        public bool IsOpen => panelRoot.activeSelf;
        public bool ShowingCategories => cardsScreen != null && cardsScreen.activeSelf;
        public BuildingCategory CurrentCategory { get; private set; }
        public bool ShowingDecorations => CurrentCategory == BuildingCategory.Decoration;
        public IReadOnlyList<BuildingCategory> Categories => _tabs.Select(t => t.Category).ToList();
        public IReadOnlyList<ShopCategoryCardView> Cards => _cards;

        /// <summary>Cards visible in the current tab (tests).</summary>
        public int VisibleItems => _items.Count(i => i.gameObject.activeSelf);

        public void Initialize(GameContent content, Wallet wallet, PlayerProgress progress,
            Func<BuildingDefinition, bool> canBuildMore, Action<BuildingDefinition> onSelected)
        {
            _content = content;
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
            BuildCards(content);
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
            if (cardsCloseButton != null)
            {
                cardsCloseButton.onClick.RemoveAllListeners();
                cardsCloseButton.onClick.AddListener(Close);
            }

            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(ShowCategories);
            }

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

            foreach (var category in CategoriesWithItems(content))
            {
                var tab = Instantiate(tabTemplate, tabsParent);
                tab.name = $"Tab_{category}";
                tab.gameObject.SetActive(true);
                tab.Bind(category, TabLabel(category), CardSprite(content, category), OpenCategory);
                _tabs.Add(tab);
            }
        }

        /// <summary>Creates one big card per category with shop items (also used by the editor preview).</summary>
        public void BuildCards(GameContent content)
        {
            if (cardTemplate == null) return;
            cardTemplate.gameObject.SetActive(false);
            foreach (var card in _cards)
                if (card != null) Destroy(card.gameObject);
            _cards.Clear();

            foreach (var category in CategoriesWithItems(content))
            {
                var card = Instantiate(cardTemplate, cardsParent);
                card.name = $"Card_{category}";
                card.gameObject.SetActive(true);
                card.Bind(category, TabLabel(category), CategoryColour(category), CardSprite(content, category), OpenCategory);
                _cards.Add(card);
            }
        }

        /// <summary>Updates the card lines and badges. <paramref name="canBuildNow"/> says if a building can be built now.</summary>
        public void RefreshCards(GameContent content, int level, Func<BuildingDefinition, bool> canBuildNow)
        {
            foreach (var card in _cards)
            {
                var defs = content.Buildings.Where(b => b != null && b.ShowInShop && b.Category == card.Category).ToList();
                bool anyUnlocked = defs.Any(d => d.UnlockLevel <= level);
                int lockedUntil = anyUnlocked || defs.Count == 0 ? 0 : defs.Min(d => d.UnlockLevel);
                card.Refresh(defs.Count, defs.Count(canBuildNow), lockedUntil);
            }
        }

        /// <summary>Highlights a tab without touching item cards (editor preview).</summary>
        public void HighlightTab(BuildingCategory category)
        {
            CurrentCategory = category;
            foreach (var tab in _tabs) tab.SetSelected(tab.Category == category, activeTabColor, idleTabColor);
        }

        /// <summary>Opens the shop on the category cards.</summary>
        public void Open()
        {
            foreach (var item in _items)
            {
                var d = item.Definition;
                item.Refresh(_progress.IsUnlocked(d.UnlockLevel), _wallet.CanAffordCoins(d.CoinCost), !CanBuildMore(d));
            }

            RefreshCards(_content, _progress.Level,
                d => _progress.IsUnlocked(d.UnlockLevel) && _wallet.CanAffordCoins(d.CoinCost) && CanBuildMore(d));
            panelRoot.SetActive(true);
            ShowCategories();
        }

        public void Close() => panelRoot.SetActive(false);

        /// <summary>Screen 1: the category cards.</summary>
        public void ShowCategories()
        {
            if (cardsScreen == null) return; // older scene without the cards screen: the list is the shop
            cardsScreen.SetActive(true);
            if (listScreen != null) listScreen.SetActive(false);
        }

        /// <summary>Screen 2 for a category (tapping a card or a tab).</summary>
        public void OpenCategory(BuildingCategory category)
        {
            if (cardsScreen != null) cardsScreen.SetActive(false);
            if (listScreen != null) listScreen.SetActive(true);
            ShowCategory(category);
        }

        /// <summary>Shows the item cards of one category.</summary>
        public void ShowCategory(BuildingCategory category)
        {
            HighlightTab(category);
            foreach (var item in _items) item.gameObject.SetActive(item.Definition.Category == category);
            if (scroll != null) scroll.horizontalNormalizedPosition = 0f;
        }

        private bool CanBuildMore(BuildingDefinition d) => _canBuildMore == null || _canBuildMore(d);

        private static IEnumerable<BuildingCategory> CategoriesWithItems(GameContent content) =>
            TabOrder.Where(c => content.Buildings.Any(b => b != null && b.ShowInShop && b.Category == c));

        private static Sprite CardSprite(GameContent content, BuildingCategory category)
        {
            if (content.TryGetBuilding(CardBuildingId(category), out var preferred) && preferred.Category == category && preferred.Sprite != null)
                return preferred.Sprite;
            var first = content.Buildings.FirstOrDefault(b => b != null && b.ShowInShop && b.Category == category);
            return first != null ? first.Sprite : null;
        }

        private void OnItemClicked(BuildingDefinition definition)
        {
            Close();
            _onSelected?.Invoke(definition);
        }
    }
}
