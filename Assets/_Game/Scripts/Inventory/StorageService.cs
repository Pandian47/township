using System;
using TownBuilder.Domain;

namespace TownBuilder
{
    /// <summary>
    /// The barn: capacity (base + storage buildings + paid expansions), selling and expanding.
    /// Plain C#, owned by the Town scene; UI and farming use it through its methods and events.
    /// </summary>
    public sealed class StorageService : IDisposable
    {
        private readonly Town _town;
        private readonly Wallet _wallet;
        private readonly GameContent _content;
        private readonly Func<string, bool> _isBuilt;

        public StorageService(Town town, Inventory inventory, Wallet wallet, StorageUpgrades upgrades, GameContent content,
            Func<string, bool> isBuilt = null)
        {
            _town = town;
            Inventory = inventory;
            _wallet = wallet;
            Upgrades = upgrades;
            _content = content;
            _isBuilt = isBuilt;

            _town.BuildingAdded += OnBuildingsChanged;
            _town.BuildingRemoved += OnBuildingsChanged;
            Upgrades.Changed += RecalculateCapacity;
            RecalculateCapacity();
        }

        /// <summary>Barn contents or expansion changed by a player action (autosave hook).</summary>
        public event Action Modified;

        public event Action<string> Notification;

        public Inventory Inventory { get; }
        public StorageUpgrades Upgrades { get; }

        public bool CanAffordExpansion => !Upgrades.IsMaxed && _wallet.CanAffordCoins(Upgrades.NextCost);

        public SellResult Sell(string itemId, int amount)
        {
            if (!_content.TryGetItem(itemId, out var item)) return SellResult.NotSellable;
            var result = Market.TrySell(Inventory, _wallet, itemId, amount, item.SellPrice, out long earned);
            if (result == SellResult.Sold)
            {
                Notification?.Invoke($"Sold {amount} {item.DisplayName}   +{earned:N0} coins");
                Modified?.Invoke();
            }

            return result;
        }

        public SellResult SellAll(string itemId) => Sell(itemId, Inventory.Count(itemId));

        public bool TryExpand()
        {
            if (!Upgrades.TryUpgrade(_wallet))
            {
                Notification?.Invoke(Upgrades.IsMaxed ? "The barn is fully expanded." : $"You need {Upgrades.NextCost:N0} coins.");
                return false;
            }

            Notification?.Invoke($"Barn expanded to {Inventory.Capacity}!");
            Modified?.Invoke();
            return true;
        }

        public void Dispose()
        {
            _town.BuildingAdded -= OnBuildingsChanged;
            _town.BuildingRemoved -= OnBuildingsChanged;
            Upgrades.Changed -= RecalculateCapacity;
        }

        private void OnBuildingsChanged(PlacedBuilding building)
        {
            if (_content.TryGetBuilding(building.DefinitionId, out var def) && def.StorageCapacityBonus > 0) RecalculateCapacity();
        }

        /// <summary>Recalculate capacity, e.g. when a storage building finishes construction.</summary>
        public void Refresh() => RecalculateCapacity();

        private void RecalculateCapacity()
        {
            int capacity = _content.BaseStorageCapacity + Upgrades.BonusCapacity;
            foreach (var b in _town.Buildings)
            {
                if (_isBuilt != null && !_isBuilt(b.InstanceId)) continue; // under construction: no bonus yet
                if (_content.TryGetBuilding(b.DefinitionId, out var def)) capacity += def.StorageCapacityBonus;
            }
            Inventory.SetCapacity(capacity);
        }
    }
}
