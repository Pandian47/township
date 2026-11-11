using System;
using System.Collections.Generic;
using System.Linq;

namespace TownBuilder.Domain
{
    /// <summary>The rules of one collection: which activity finds its items, how often, and its item ids.</summary>
    public sealed class CollectionSetSpec
    {
        public CollectionSetSpec(string id, AchievementKind source, double dropChance, IEnumerable<string> itemIds)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Collection id is required.", nameof(id));
            Id = id;
            Source = source;
            DropChance = Math.Max(0, Math.Min(1, dropChance));
            ItemIds = (itemIds ?? Enumerable.Empty<string>()).Where(i => !string.IsNullOrEmpty(i)).Distinct().ToList();
            if (ItemIds.Count == 0) throw new ArgumentException($"Collection {id} needs at least one item.", nameof(itemIds));
        }

        public string Id { get; }
        /// <summary>HarvestCrops (per harvest), MakeProducts (per factory collect) or DeliverOrders (per order).</summary>
        public AchievementKind Source { get; }
        /// <summary>Chance (0-1) that one such action finds an item of this collection.</summary>
        public double DropChance { get; }
        public IReadOnlyList<string> ItemIds { get; }
    }

    /// <summary>
    /// Rare collection items the player has found (kept apart from the barn) and how often each set was completed.
    /// Completing a set uses one of each of its items; sets can be completed again and again.
    /// </summary>
    public sealed class CollectionBook
    {
        private readonly List<CollectionSetSpec> _sets;
        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _completions = new Dictionary<string, int>();

        public CollectionBook(IEnumerable<CollectionSetSpec> sets)
        {
            _sets = new List<CollectionSetSpec>();
            foreach (var s in sets ?? Enumerable.Empty<CollectionSetSpec>())
                if (s != null && _sets.All(x => x.Id != s.Id)) _sets.Add(s);
        }

        public IReadOnlyList<CollectionSetSpec> Sets => _sets;
        public IEnumerable<KeyValuePair<string, int>> Counts => _counts;
        public IEnumerable<KeyValuePair<string, int>> AllCompletions => _completions;

        public bool TryGet(string setId, out CollectionSetSpec set)
        {
            set = _sets.FirstOrDefault(s => s.Id == setId);
            return set != null;
        }

        public int Count(string itemId) => itemId != null && _counts.TryGetValue(itemId, out var n) ? n : 0;
        public int Completions(string setId) => setId != null && _completions.TryGetValue(setId, out var n) ? n : 0;

        /// <summary>Different items of a set the player has (at least one of).</summary>
        public int DistinctOwned(string setId) => TryGet(setId, out var s) ? s.ItemIds.Count(i => Count(i) > 0) : 0;

        public bool CanComplete(string setId) => TryGet(setId, out var s) && s.ItemIds.All(i => Count(i) > 0);

        public int CompletableCount => _sets.Count(s => CanComplete(s.Id));

        public void Add(string itemId, int amount = 1)
        {
            if (string.IsNullOrEmpty(itemId) || amount <= 0) return;
            _counts[itemId] = Count(itemId) + amount;
        }

        /// <summary>Uses one of each item of the set. False when one is missing.</summary>
        public bool TryComplete(string setId)
        {
            if (!CanComplete(setId)) return false;
            TryGet(setId, out var set);
            foreach (var i in set.ItemIds)
            {
                int left = Count(i) - 1;
                if (left > 0) _counts[i] = left;
                else _counts.Remove(i);
            }

            _completions[setId] = Completions(setId) + 1;
            return true;
        }

        /// <summary>
        /// One action of kind <paramref name="source"/> happened: each collection fed by it gets a chance to drop one of
        /// its items (each item equally likely). Returns the item found, or null.
        /// </summary>
        public string Roll(AchievementKind source, Random random)
        {
            foreach (var set in _sets)
            {
                if (set.Source != source || random.NextDouble() >= set.DropChance) continue;
                string item = set.ItemIds[random.Next(set.ItemIds.Count)];
                Add(item);
                return item;
            }

            return null;
        }

        public void Restore(IEnumerable<KeyValuePair<string, int>> counts, IEnumerable<KeyValuePair<string, int>> completions)
        {
            _counts.Clear();
            _completions.Clear();
            var known = new HashSet<string>(_sets.SelectMany(s => s.ItemIds));
            foreach (var c in counts ?? Enumerable.Empty<KeyValuePair<string, int>>())
                if (known.Contains(c.Key) && c.Value > 0) _counts[c.Key] = c.Value;
            foreach (var c in completions ?? Enumerable.Empty<KeyValuePair<string, int>>())
                if (TryGet(c.Key, out _) && c.Value > 0) _completions[c.Key] = c.Value;
        }
    }
}
