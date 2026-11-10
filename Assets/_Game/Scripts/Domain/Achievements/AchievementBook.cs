using System;
using System.Collections.Generic;
using System.Linq;

namespace TownBuilder.Domain
{
    /// <summary>What an achievement counts. Saved by name: never rename a value without a save migration.</summary>
    public enum AchievementKind
    {
        /// <summary>Crops harvested (any crop, counted per item).</summary>
        HarvestCrops,
        /// <summary>Products collected from factories and animals (counted per item).</summary>
        MakeProducts,
        /// <summary>Orders delivered.</summary>
        DeliverOrders,
        /// <summary>Train crates loaded.</summary>
        LoadCrates,
        /// <summary>Buildings owned (highest so far; the adapter decides what counts).</summary>
        OwnBuildings,
        /// <summary>Plots of land bought.</summary>
        BuyLand,
        /// <summary>Player level reached.</summary>
        ReachLevel,
    }

    /// <summary>The rules of one achievement: a kind and ascending tier goals (e.g. 50, 300, 1500).</summary>
    public sealed class AchievementSpec
    {
        public AchievementSpec(string id, AchievementKind kind, IEnumerable<int> goals)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Achievement id is required.", nameof(id));
            Id = id;
            Kind = kind;
            var list = (goals ?? Enumerable.Empty<int>()).ToList();
            if (list.Count == 0) throw new ArgumentException("An achievement needs at least one tier.", nameof(goals));
            for (int i = 0; i < list.Count; i++)
                if (list[i] < 1 || (i > 0 && list[i] <= list[i - 1]))
                    throw new ArgumentException($"Achievement {id}: tier goals must be positive and ascending.", nameof(goals));
            Goals = list;
        }

        public string Id { get; }
        public AchievementKind Kind { get; }
        public IReadOnlyList<int> Goals { get; }
        public int TierCount => Goals.Count;
    }

    /// <summary>
    /// Long-term goals in tiers. Counters are kept per kind (so a new achievement of an existing kind sees the progress
    /// already made); each achievement remembers how many tiers were claimed. A tier is claimable once its counter reaches
    /// the next goal; tiers are claimed in order.
    /// </summary>
    public sealed class AchievementBook
    {
        private readonly List<AchievementSpec> _specs;
        private readonly Dictionary<string, AchievementSpec> _byId;
        private readonly Dictionary<AchievementKind, long> _counts = new Dictionary<AchievementKind, long>();
        private readonly Dictionary<string, int> _claimed = new Dictionary<string, int>();

        public AchievementBook(IEnumerable<AchievementSpec> specs)
        {
            _specs = new List<AchievementSpec>();
            _byId = new Dictionary<string, AchievementSpec>();
            foreach (var s in specs ?? Enumerable.Empty<AchievementSpec>())
            {
                if (s == null || _byId.ContainsKey(s.Id)) continue;
                _specs.Add(s);
                _byId.Add(s.Id, s);
            }
        }

        /// <summary>An achievement reached its next unclaimed goal (id).</summary>
        public event Action<string> GoalReached;
        /// <summary>A counter or a claim changed.</summary>
        public event Action Changed;

        public IReadOnlyList<AchievementSpec> Specs => _specs;

        public bool TryGet(string id, out AchievementSpec spec) => _byId.TryGetValue(id ?? "", out spec);

        public long Count(AchievementKind kind) => _counts.TryGetValue(kind, out var v) ? v : 0;

        public int ClaimedTiers(string id) => _claimed.TryGetValue(id ?? "", out var t) ? t : 0;

        public bool IsFinished(string id) => TryGet(id, out var s) && ClaimedTiers(id) >= s.TierCount;

        /// <summary>Goal of the next unclaimed tier, or -1 when every tier is claimed (or the id is unknown).</summary>
        public int NextGoal(string id)
        {
            if (!TryGet(id, out var s)) return -1;
            int tier = ClaimedTiers(id);
            return tier < s.TierCount ? s.Goals[tier] : -1;
        }

        public bool CanClaim(string id)
        {
            int goal = NextGoal(id);
            return goal > 0 && Count(_byId[id].Kind) >= goal;
        }

        /// <summary>How many achievements have a tier waiting to be claimed.</summary>
        public int ClaimableCount => _specs.Count(s => CanClaim(s.Id));

        /// <summary>Adds to a counter (harvests, orders, crates...).</summary>
        public void Add(AchievementKind kind, long amount)
        {
            if (amount <= 0) return;
            SetCount(kind, Count(kind) + amount);
        }

        /// <summary>Raises a "highest so far" counter (level, buildings owned). Never lowers it.</summary>
        public void RaiseTo(AchievementKind kind, long value)
        {
            if (value <= Count(kind)) return;
            SetCount(kind, value);
        }

        /// <summary>Claims the next tier. <paramref name="tier"/> is its 0-based index.</summary>
        public bool TryClaim(string id, out int tier)
        {
            tier = -1;
            if (!CanClaim(id)) return false;
            tier = ClaimedTiers(id);
            _claimed[id] = tier + 1;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Saved state. Unknown achievement ids are ignored; claimed tiers are clamped to the tier count.</summary>
        public void Restore(IEnumerable<KeyValuePair<AchievementKind, long>> counts, IEnumerable<KeyValuePair<string, int>> claimed)
        {
            _counts.Clear();
            _claimed.Clear();
            foreach (var c in counts ?? Enumerable.Empty<KeyValuePair<AchievementKind, long>>())
                if (c.Value > 0) _counts[c.Key] = c.Value;
            foreach (var c in claimed ?? Enumerable.Empty<KeyValuePair<string, int>>())
                if (TryGet(c.Key, out var s) && c.Value > 0) _claimed[c.Key] = Math.Min(c.Value, s.TierCount);
        }

        public IEnumerable<KeyValuePair<AchievementKind, long>> Counts => _counts;
        public IEnumerable<KeyValuePair<string, int>> Claimed => _claimed;

        private void SetCount(AchievementKind kind, long value)
        {
            long before = Count(kind);
            _counts[kind] = value;
            foreach (var s in _specs)
            {
                if (s.Kind != kind) continue;
                int goal = NextGoal(s.Id);
                if (goal > 0 && before < goal && value >= goal) GoalReached?.Invoke(s.Id);
            }

            Changed?.Invoke();
        }
    }
}
