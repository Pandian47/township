using System;
using System.Collections.Generic;

namespace TownBuilder.Domain
{
    /// <summary>
    /// Items the player owns (crops, later products). Total stored amount is limited by <see cref="Capacity"/>
    /// (the barn). Capacity can drop below the stored total (e.g. after removing a barn); then nothing new
    /// can be added until items are used, but nothing is destroyed.
    /// </summary>
    public sealed class Inventory
    {
        private readonly Dictionary<string, int> _items = new Dictionary<string, int>();

        public Inventory(int capacity)
        {
            Capacity = Math.Max(0, capacity);
        }

        public event Action Changed;

        public int Capacity { get; private set; }
        public int Total { get; private set; }
        public int FreeSpace => Math.Max(0, Capacity - Total);
        public IReadOnlyDictionary<string, int> Items => _items;

        public int Count(string itemId) => itemId != null && _items.TryGetValue(itemId, out var n) ? n : 0;

        public bool CanAdd(int amount) => amount >= 0 && amount <= FreeSpace;

        public bool TryAdd(string itemId, int amount)
        {
            if (string.IsNullOrEmpty(itemId)) throw new ArgumentException("Item id is required.", nameof(itemId));
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (!CanAdd(amount)) return false;
            if (amount == 0) return true;

            _items[itemId] = Count(itemId) + amount;
            Total += amount;
            Changed?.Invoke();
            return true;
        }

        public bool TryRemove(string itemId, int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            int have = Count(itemId);
            if (have < amount) return false;
            if (amount == 0) return true;

            if (have == amount) _items.Remove(itemId);
            else _items[itemId] = have - amount;
            Total -= amount;
            Changed?.Invoke();
            return true;
        }

        public void SetCapacity(int capacity)
        {
            capacity = Math.Max(0, capacity);
            if (capacity == Capacity) return;
            Capacity = capacity;
            Changed?.Invoke();
        }

        /// <summary>Loads saved amounts, ignoring the capacity limit (never lose items on load).</summary>
        public void Restore(string itemId, int amount)
        {
            if (string.IsNullOrEmpty(itemId) || amount <= 0) return;
            _items[itemId] = Count(itemId) + amount;
            Total += amount;
            Changed?.Invoke();
        }
    }
}
