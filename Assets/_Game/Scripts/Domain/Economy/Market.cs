using System;

namespace TownBuilder.Domain
{
    public enum SellResult
    {
        Sold,
        NotEnoughItems,
        NotSellable,
        InvalidAmount,
    }

    /// <summary>Barn transactions that touch both the inventory and the wallet; either both change or neither.</summary>
    public static class Market
    {
        public static SellResult TrySell(Inventory inventory, Wallet wallet, string itemId, int amount, int unitPrice, out long coinsEarned)
        {
            coinsEarned = 0;
            if (amount <= 0) return SellResult.InvalidAmount;
            if (unitPrice <= 0) return SellResult.NotSellable;
            if (inventory.Count(itemId) < amount) return SellResult.NotEnoughItems;

            coinsEarned = checked((long)amount * unitPrice);
            inventory.TryRemove(itemId, amount);
            wallet.AddCoins(coinsEarned);
            return SellResult.Sold;
        }
    }

    /// <summary>
    /// Paid barn expansion. Each level adds <see cref="CapacityPerLevel"/>; cost grows linearly:
    /// level 1 costs BaseCost, level 2 costs 2 x BaseCost, and so on.
    /// </summary>
    public sealed class StorageUpgrades
    {
        public StorageUpgrades(int level, int capacityPerLevel, int baseCost, int maxLevel)
        {
            if (capacityPerLevel < 0) throw new ArgumentOutOfRangeException(nameof(capacityPerLevel));
            if (baseCost < 0) throw new ArgumentOutOfRangeException(nameof(baseCost));
            MaxLevel = Math.Max(0, maxLevel);
            Level = Math.Max(0, Math.Min(level, MaxLevel));
            CapacityPerLevel = capacityPerLevel;
            BaseCost = baseCost;
        }

        public event Action Changed;

        public int Level { get; private set; }
        public int MaxLevel { get; }
        public int CapacityPerLevel { get; }
        public int BaseCost { get; }
        public int BonusCapacity => Level * CapacityPerLevel;
        public bool IsMaxed => Level >= MaxLevel;
        public long NextCost => (long)BaseCost * (Level + 1);

        public bool TryUpgrade(Wallet wallet)
        {
            if (IsMaxed) return false;
            if (!wallet.TrySpendCoins(NextCost)) return false;
            Level++;
            Changed?.Invoke();
            return true;
        }
    }
}
