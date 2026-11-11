using System;
using System.Collections.Generic;
using System.Linq;
using TownBuilder.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TownBuilder
{
    /// <summary>
    /// The Build shop, in two full windows:
    ///   1. Category cards (Houses, Town, Factories, Farm, Decorations): big picture cards with a coloured ribbon and a
    ///      badge counting what can be built right now.
    ///   2. One category's buildings: the sign shows the category name, big cards scroll sideways, Back returns.
    /// Category cards and building cards are cloned from hidden templates, so new content needs no scene changes.
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
        [Tooltip("Ribbon art per category, in TabOrder (Houses, Town, Factories, Farm, Decorations). Empty = tinted template ribbon.")]
        [SerializeField] private Sprite[] cardRibbons = new Sprite[0];
        [Tooltip("Card picture per category, in TabOrder. Empty = the category's building sprite.")]
        [SerializeField] private Sprite[] cardPictures = new Sprite[0];
        [Header("Screen 2: buildings of one category")]
        [SerializeField] private GameObject listScreen;
        [SerializeField] private Button backButton;
        [SerializeField] private Transform itemsParent;
        [SerializeField] private ShopItemView itemTemplate;
        [SerializeField] private Button closeButton;
        [Tooltip("The list window's sign text (category name).")]
        [SerializeField] private TMP_Text listTitle;
        [SerializeField] private ScrollRect scroll;

        private readonly List<ShopItemView> _items = new List<ShopItemView>();
        private readonly List<BuildingCategory> _categories = new List<BuildingCategory>();
        private readonly List<ShopCategoryCardView> _cards = new List<ShopCategoryCardView>();
        private GameContent _content;
        private Wallet _wallet;
        private PlayerProgress _progress;
        private Action<BuildingDefinition> _onSelected;
        private Func<BuildingDefinition, bool> _canBuildMore;
        private Func<BuildingDefinition, bool> _isListed = _ => true;
        private Func<BuildingDefinition, int> _ownedCount;

        /// <summary>
        /// Hides cards that aren't for sale right now (event-only decorations not unlocked yet, other seasons' decorations).
        /// Filters add up: a card shows only when every filter allows it.
        /// </summary>
        public void AddListingFilter(Func<BuildingDefinition, bool> isListed)
        {
            if (isListed == null) return;
            var previous = _isListed;
            _isListed = d => previous(d) && isListed(d);
        }

        public bool IsOpen => panelRoot.activeSelf;
        public bool ShowingCategories => cardsScreen != null && cardsScreen.activeSelf;
        public BuildingCategory CurrentCategory { get; private set; }
        public bool ShowingDecorations => CurrentCategory == BuildingCategory.Decoration;
        public IReadOnlyList<BuildingCategory> Categories => _categories;
        public IReadOnlyList<ShopCategoryCardView> Cards => _cards;

        /// <summary>Building cards visible in the open category (tests).</summary>
        public int VisibleItems => _items.Count(i => i.gameObject.activeSelf);

        public void Initialize(GameContent content, Wallet wallet, PlayerProgress progress,
            Func<BuildingDefinition, bool> canBuildMore, Action<BuildingDefinition> onSelected, Func<BuildingDefinition, int> ownedCount = null)
        {
            _ownedCount = ownedCount;
            _content = content;
            _wallet = wallet;
            _progress = progress;
            _canBuildMore = canBuildMore;
            _onSelected = onSelected;

            itemTemplate.gameObject.SetActive(false);
            foreach (var item in _items) Destroy(item.gameObject);
            _items.Clear();

            foreach (var definition in content.Buildings.Where(b => b != null).OrderByDescending(b => b.IsLandscapeTool))
            {
                if (definition == null || !definition.ShowInShop) continue;
                var item = Instantiate(itemTemplate, itemsParent);
                item.gameObject.SetActive(true);
                item.Bind(definition, OnItemClicked);
                _items.Add(item);
            }

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

            if (_categories.Count > 0) ShowCategory(_categories[0]);
            Close();
        }

        /// <summary>Creates one big card per category with shop items (also used by the editor preview).</summary>
        public void BuildCards(GameContent content)
        {
            if (cardTemplate == null) return;
            cardTemplate.gameObject.SetActive(false);
            foreach (var card in _cards)
                if (card != null) Destroy(card.gameObject);
            _cards.Clear();
            _categories.Clear();
            _categories.AddRange(CategoriesWithItems(content));

            foreach (var category in CategoriesWithItems(content))
            {
                var card = Instantiate(cardTemplate, cardsParent);
                card.name = $"Card_{category}";
                card.gameObject.SetActive(true);
                int i = Array.IndexOf(TabOrder, category);
                var ribbon = i >= 0 && i < cardRibbons.Length ? cardRibbons[i] : null;
                var picture = i >= 0 && i < cardPictures.Length && cardPictures[i] != null ? cardPictures[i] : CardSprite(content, category);
                card.Bind(category, TabLabel(category), ribbon, CategoryColour(category), picture, OpenCategory);
                _cards.Add(card);
            }
        }

        /// <summary>Updates the card lines and badges. <paramref name="canBuildNow"/> says if a building can be built now.</summary>
        public void RefreshCards(GameContent content, int level, Func<BuildingDefinition, bool> canBuildNow,
            Func<BuildingDefinition, bool> isListed = null)
        {
            foreach (var card in _cards)
            {
                var defs = content.Buildings.Where(b => b != null && b.ShowInShop && b.Category == card.Category
                                                        && (isListed == null ? !b.EventOnly : isListed(b))).ToList();
                bool anyUnlocked = defs.Any(d => d.UnlockLevel <= level);
                int lockedUntil = anyUnlocked || defs.Count == 0 ? 0 : defs.Min(d => d.UnlockLevel);
                card.Refresh(defs.Count, defs.Count(canBuildNow), lockedUntil);
            }
        }

        /// <summary>Sets the open category and the sign text without touching the building cards (editor preview).</summary>
        public void SetTitle(BuildingCategory category)
        {
            CurrentCategory = category;
            if (listTitle != null) listTitle.text = TabLabel(category).ToUpperInvariant();
        }

        /// <summary>Opens the shop on the category cards.</summary>
        public void Open()
        {
            foreach (var item in _items)
            {
                var d = item.Definition;
                item.Refresh(_progress.IsUnlocked(d.UnlockLevel), _wallet.CanAffordCoins(d.CoinCost), !CanBuildMore(d),
                    _ownedCount != null ? _ownedCount(d) : 0);
            }

            RefreshCards(_content, _progress.Level,
                d => _progress.IsUnlocked(d.UnlockLevel) && _wallet.CanAffordCoins(d.CoinCost) && CanBuildMore(d), _isListed);
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

        /// <summary>Screen 2 for a category (tapping a category card).</summary>
        public void OpenCategory(BuildingCategory category)
        {
            if (cardsScreen != null) cardsScreen.SetActive(false);
            if (listScreen != null) listScreen.SetActive(true);
            ShowCategory(category);
        }

        /// <summary>Shows the item cards of one category.</summary>
        public void ShowCategory(BuildingCategory category)
        {
            SetTitle(category);
            foreach (var item in _items) item.gameObject.SetActive(item.Definition.Category == category && _isListed(item.Definition));
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
