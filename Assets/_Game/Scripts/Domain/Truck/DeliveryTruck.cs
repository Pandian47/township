using System;
using System.Collections.Generic;
using System.Linq;

namespace TownBuilder.Domain
{
    public enum TruckState
    {
        /// <summary>Parked at the depot with loads to fill (no time limit).</summary>
        AtDepot,
        /// <summary>On a delivery trip; comes back at <see cref="DeliveryTruck.ReturnsAtUnixMs"/> with the earnings.</summary>
        Away,
    }

    public enum TruckLoadResult
    {
        Loaded,
        NotAtDepot,
        NoSuchLoad,
        AlreadyLoaded,
        MissingItems,
    }

    /// <summary>One item line of a truck load: N of an item.</summary>
    public sealed class TruckLine
    {
        public TruckLine(string itemId, int amount)
        {
            ItemId = itemId;
            Amount = Math.Max(1, amount);
        }

        public string ItemId { get; }
        public int Amount { get; }
    }

    /// <summary>A load the truck asks for: several item lines, delivered together for coins, XP and a building material.</summary>
    public sealed class TruckLoad
    {
        public TruckLoad(IEnumerable<TruckLine> lines, long rewardCoins, long rewardXp, string materialId = null, int materialAmount = 0,
            bool loaded = false)
        {
            Lines = (lines ?? Enumerable.Empty<TruckLine>()).ToList();
            RewardCoins = Math.Max(0, rewardCoins);
            RewardXp = Math.Max(0, rewardXp);
            MaterialId = string.IsNullOrEmpty(materialId) || materialAmount <= 0 ? null : materialId;
            MaterialAmount = MaterialId == null ? 0 : materialAmount;
            Loaded = loaded;
        }

        public IReadOnlyList<TruckLine> Lines { get; }
        public long RewardCoins { get; }
        public long RewardXp { get; }
        public string MaterialId { get; }
        public int MaterialAmount { get; }
        public bool Loaded { get; internal set; }
    }

    /// <summary>What the truck brings back from a trip.</summary>
    public sealed class TruckPayout
    {
        public long Coins;
        public long Xp;
        public readonly Dictionary<string, int> Materials = new Dictionary<string, int>();
        public bool IsEmpty => Coins == 0 && Xp == 0 && Materials.Count == 0;
    }

    /// <summary>
    /// The delivery truck. Different rhythm from the train: it asks for a few bigger, mixed loads; the player loads what
    /// they can and sends it on a long trip; the earnings (plus a full-truck bonus) are paid when it comes back.
    /// Times are UTC timestamps, so the trip goes on while the app is closed. A new truck starts "away, back at 0",
    /// i.e. it is at the depot as soon as there is one.
    /// </summary>
    public sealed class DeliveryTruck
    {
        private readonly List<TruckLoad> _loads = new List<TruckLoad>();

        public TruckState State { get; private set; } = TruckState.Away;
        public long ReturnsAtUnixMs { get; private set; }
        public IReadOnlyList<TruckLoad> Loads => _loads;
        public bool IsAtDepot => State == TruckState.AtDepot;

        /// <summary>Earnings carried on the current trip (paid on return).</summary>
        public TruckPayout Cargo { get; private set; } = new TruckPayout();

        public int LoadedCount => _loads.Count(l => l.Loaded);
        public bool AllLoaded => _loads.Count > 0 && LoadedCount == _loads.Count;
        public bool CanSend => IsAtDepot && LoadedCount > 0;

        public long RemainingMs(long nowUnixMs) => State == TruckState.Away ? Math.Max(0, ReturnsAtUnixMs - nowUnixMs) : 0;

        /// <summary>True when every line of a load is in the inventory.</summary>
        public bool HasItemsFor(int index, Inventory inventory) =>
            index >= 0 && index < _loads.Count && _loads[index].Lines.All(l => inventory.Count(l.ItemId) >= l.Amount);

        /// <summary>Takes all of a load's items from the inventory at once (nothing is taken if one line is short).</summary>
        public TruckLoadResult TryLoad(int index, Inventory inventory)
        {
            if (State != TruckState.AtDepot) return TruckLoadResult.NotAtDepot;
            if (index < 0 || index >= _loads.Count) return TruckLoadResult.NoSuchLoad;
            var load = _loads[index];
            if (load.Loaded) return TruckLoadResult.AlreadyLoaded;
            if (!HasItemsFor(index, inventory)) return TruckLoadResult.MissingItems;
            foreach (var line in load.Lines) inventory.TryRemove(line.ItemId, line.Amount);
            load.Loaded = true;
            return TruckLoadResult.Loaded;
        }

        /// <summary>Bonus coins for a completely loaded truck: a share of all load rewards.</summary>
        public long FullBonus(double fraction) =>
            (long)Math.Round(_loads.Sum(l => l.RewardCoins) * Math.Max(0, fraction), MidpointRounding.AwayFromZero);

        /// <summary>Sends the truck off with at least one load. Its earnings travel with it and are paid on return.</summary>
        public bool SendOff(long nowUnixMs, long tripMs, double fullBonusFraction)
        {
            if (!CanSend) return false;
            var cargo = new TruckPayout();
            foreach (var load in _loads.Where(l => l.Loaded))
            {
                cargo.Coins += load.RewardCoins;
                cargo.Xp += load.RewardXp;
                if (load.MaterialId != null)
                    cargo.Materials[load.MaterialId] = (cargo.Materials.TryGetValue(load.MaterialId, out var n) ? n : 0) + load.MaterialAmount;
            }

            if (AllLoaded) cargo.Coins += FullBonus(fullBonusFraction);
            Cargo = cargo;
            _loads.Clear();
            State = TruckState.Away;
            ReturnsAtUnixMs = nowUnixMs + Math.Max(0, tripMs);
            return true;
        }

        /// <summary>
        /// If the truck is due back, it parks with new loads from <paramref name="makeLoads"/> and hands over its cargo
        /// (null when it wasn't due). The caller pays the cargo.
        /// </summary>
        public TruckPayout UpdateReturn(long nowUnixMs, Func<IReadOnlyList<TruckLoad>> makeLoads)
        {
            if (State != TruckState.Away || nowUnixMs < ReturnsAtUnixMs) return null;
            var cargo = Cargo;
            Cargo = new TruckPayout();
            _loads.Clear();
            _loads.AddRange(makeLoads());
            State = TruckState.AtDepot;
            return cargo;
        }

        /// <summary>Brings the truck back now (rewarded ad or gems). It returns on the next update.</summary>
        public bool ReturnNow(long nowUnixMs)
        {
            if (State != TruckState.Away || ReturnsAtUnixMs <= nowUnixMs) return false;
            ReturnsAtUnixMs = nowUnixMs;
            return true;
        }

        public void Restore(TruckState state, long returnsAtUnixMs, IEnumerable<TruckLoad> loads, TruckPayout cargo)
        {
            State = state;
            ReturnsAtUnixMs = returnsAtUnixMs;
            _loads.Clear();
            if (loads != null) _loads.AddRange(loads);
            Cargo = cargo ?? new TruckPayout();
            if (State == TruckState.AtDepot && _loads.Count == 0) State = TruckState.Away; // nothing to load: come back fresh
        }
    }

    /// <summary>Builds truck loads from what the player can make: a few different items per load, bigger amounts than the train.</summary>
    public static class TruckLoadGenerator
    {
        public static List<TruckLoad> Generate(IReadOnlyList<OrderCandidate> candidates, int count, int linesPerLoad, double coinMultiplier,
            int xpDivisor, Random random, IReadOnlyList<string> materialIds = null)
        {
            var loads = new List<TruckLoad>();
            if (candidates == null || candidates.Count == 0) return loads;
            for (int i = 0; i < count; i++)
            {
                var picked = candidates.OrderBy(_ => random.Next()).Take(Math.Max(1, Math.Min(linesPerLoad, candidates.Count))).ToList();
                var lines = new List<TruckLine>();
                long value = 0;
                foreach (var c in picked)
                {
                    int amount = c.UnitValue <= 10 ? random.Next(5, 9) : c.UnitValue <= 40 ? random.Next(3, 6) : random.Next(2, 4);
                    lines.Add(new TruckLine(c.ItemId, amount));
                    value += (long)c.UnitValue * amount;
                }

                long coins = (long)Math.Ceiling(value * coinMultiplier);
                long xp = Math.Max(1, value / Math.Max(1, xpDivisor));
                string material = materialIds != null && materialIds.Count > 0 ? materialIds[random.Next(materialIds.Count)] : null;
                loads.Add(new TruckLoad(lines, coins, xp, material, material != null ? 1 : 0));
            }

            return loads;
        }
    }
}
