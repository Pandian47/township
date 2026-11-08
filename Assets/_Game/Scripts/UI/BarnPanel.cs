using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TownBuilder
{
    /// <summary>
    /// Barn screen: every stored item with its amount and sell buttons, current usage, and the paid expansion.
    /// Item cards are cloned from a hidden template and rebuilt whenever the inventory changes while open.
    /// </summary>
    public sealed class BarnPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Transform itemsParent;
        [SerializeField] private BarnItemView itemTemplate;
        [SerializeField] private TMP_Text emptyText;
        [SerializeField] private Button expandButton;
        [SerializeField] private TMP_Text expandLabel;
        [SerializeField] private Button closeButton;

        private readonly List<BarnItemView> _items = new List<BarnItemView>();
        private StorageService _storage;
        private GameContent _content;

        public bool IsOpen => panelRoot.activeSelf;

        public void Initialize(StorageService storage, GameContent content)
        {
            _storage = storage;
            _content = content;
            itemTemplate.gameObject.SetActive(false);
            expandButton.onClick.AddListener(() => _storage.TryExpand());
            closeButton.onClick.AddListener(Close);
            _storage.Inventory.Changed += OnInventoryChanged;
            Close();
        }

        private void OnDestroy()
        {
            if (_storage != null) _storage.Inventory.Changed -= OnInventoryChanged;
        }

        public void Open()
        {
            panelRoot.SetActive(true);
            Rebuild();
        }

        public void Close() => panelRoot.SetActive(false);

        private void OnInventoryChanged()
        {
            if (IsOpen) Rebuild();
        }

        private void Rebuild()
        {
            var inventory = _storage.Inventory;
            titleText.text = $"Barn   {inventory.Total}/{inventory.Capacity}";

            var entries = inventory.Items
                .Where(p => p.Value > 0)
                .OrderBy(p => _content.ItemName(p.Key))
                .ToList();

            while (_items.Count < entries.Count)
            {
                var view = Instantiate(itemTemplate, itemsParent);
                _items.Add(view);
            }

            for (int i = 0; i < _items.Count; i++)
            {
                bool used = i < entries.Count;
                _items[i].gameObject.SetActive(used);
                if (!used) continue;

                var (itemId, amount) = (entries[i].Key, entries[i].Value);
                _content.TryGetItem(itemId, out var item);
                _items[i].Show(itemId, item, amount, id => _storage.Sell(id, 1), id => _storage.SellAll(id));
            }

            emptyText.gameObject.SetActive(entries.Count == 0);

            var upgrades = _storage.Upgrades;
            if (upgrades.IsMaxed)
            {
                expandLabel.text = "Fully expanded";
                expandButton.interactable = false;
            }
            else
            {
                expandLabel.text = $"Expand +{upgrades.CapacityPerLevel}\n{upgrades.NextCost:N0} coins";
                expandButton.interactable = _storage.CanAffordExpansion;
            }
        }
    }
}
