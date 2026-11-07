using System;
using System.Collections.Generic;

namespace TownBuilder.Domain
{
    public readonly struct OrderLine
    {
        public OrderLine(string itemId, int amount)
        {
            ItemId = itemId;
            Amount = amount;
        }

        public string ItemId { get; }
        public int Amount { get; }
    }

    /// <summary>A customer order: deliver all lines to get the reward. Unavailable until <see cref="AvailableAtUnixMs"/> (after a skip).</summary>
    public sealed class Order
    {
        public Order(IReadOnlyList<OrderLine> lines, long rewardCoins, long rewardXp, long availableAtUnixMs = 0)
        {
            Lines = lines ?? throw new ArgumentNullException(nameof(lines));
            RewardCoins = Math.Max(0, rewardCoins);
            RewardXp = Math.Max(0, rewardXp);
            AvailableAtUnixMs = availableAtUnixMs;
        }

        public IReadOnlyList<OrderLine> Lines { get; }
        public long RewardCoins { get; }
        public long RewardXp { get; }
        public long AvailableAtUnixMs { get; }

        public bool IsAvailable(long nowUnixMs) => nowUnixMs >= AvailableAtUnixMs;
    }

    /// <summary>Item that can appear in orders, with its value (sell price) used to size rewards.</summary>
    public readonly struct OrderCandidate
    {
        public OrderCandidate(string itemId, int unitValue)
        {
            ItemId = itemId;
            UnitValue = Math.Max(1, unitValue);
        }

        public string ItemId { get; }
        public int UnitValue { get; }
    }

    /// <summary>
    /// Creates orders from the items the player can currently make. Rewards are always worth more than selling the
    /// same items in the barn (<see cref="CoinMultiplier"/>), so delivering orders is the better choice.
    /// </summary>
    public sealed class OrderGenerator
    {
        public double CoinMultiplier { get; set; } = 1.6;
        public int CoinBonus { get; set; } = 5;
        public double XpPerValue { get; set; } = 0.25;

        public Order Generate(IReadOnlyList<OrderCandidate> candidates, int playerLevel, Random random, long availableAtUnixMs = 0)
        {
            if (candidates == null || candidates.Count == 0) throw new ArgumentException("No order candidates.", nameof(candidates));

            int maxLines = Math.Min(Math.Min(3, candidates.Count), 1 + playerLevel / 2);
            int lineCount = random.Next(1, maxLines + 1);

            // Pick distinct items.
            var pool = new List<OrderCandidate>(candidates);
            var lines = new List<OrderLine>(lineCount);
            long value = 0;
            for (int i = 0; i < lineCount; i++)
            {
                int pick = random.Next(pool.Count);
                var c = pool[pick];
                pool.RemoveAt(pick);

                // Cheap items in bigger amounts, valuable (processed) items in small amounts.
                int maxAmount = c.UnitValue >= 10 ? 2 : 2 + playerLevel / 2;
                int amount = random.Next(1, Math.Max(1, maxAmount) + 1);
                lines.Add(new OrderLine(c.ItemId, amount));
                value += (long)c.UnitValue * amount;
            }

            long coins = (long)Math.Round(value * CoinMultiplier) + CoinBonus;
            long xp = Math.Max(1, (long)Math.Round(value * XpPerValue));
            return new Order(lines, coins, xp, availableAtUnixMs);
        }
    }

    public enum DeliverResult
    {
        Delivered,
        MissingItems,
        NotAvailable,
        InvalidSlot,
    }

    /// <summary>Fixed number of order slots. A slot can be empty (null) until the runtime fills it.</summary>
    public sealed class OrderBoard
    {
        private readonly Order[] _slots;

        public OrderBoard(int slots)
        {
            _slots = new Order[Math.Max(1, slots)];
        }

        public event Action Changed;

        public int SlotCount => _slots.Length;

        public Order Get(int slot) => slot >= 0 && slot < _slots.Length ? _slots[slot] : null;

        public void Set(int slot, Order order)
        {
            if (slot < 0 || slot >= _slots.Length) throw new ArgumentOutOfRangeException(nameof(slot));
            _slots[slot] = order;
            Changed?.Invoke();
        }

        public bool CanDeliver(int slot, Inventory inventory, long nowUnixMs)
        {
            var order = Get(slot);
            if (order == null || !order.IsAvailable(nowUnixMs)) return false;
            foreach (var line in order.Lines)
                if (inventory.Count(line.ItemId) < line.Amount) return false;
            return true;
        }

        /// <summary>Takes the items, pays coins and XP, and empties the slot (the caller generates the next order).</summary>
        public DeliverResult TryDeliver(int slot, Inventory inventory, Wallet wallet, PlayerProgress progress, long nowUnixMs)
        {
            var order = Get(slot);
            if (order == null) return DeliverResult.InvalidSlot;
            if (!order.IsAvailable(nowUnixMs)) return DeliverResult.NotAvailable;
            if (!CanDeliver(slot, inventory, nowUnixMs)) return DeliverResult.MissingItems;

            foreach (var line in order.Lines) inventory.TryRemove(line.ItemId, line.Amount);
            _slots[slot] = null;
            wallet.AddCoins(order.RewardCoins);
            progress.AddXp(order.RewardXp);
            Changed?.Invoke();
            return DeliverResult.Delivered;
        }
    }
}
