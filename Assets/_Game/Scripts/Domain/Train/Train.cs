using System;
using System.Collections.Generic;

namespace TownBuilder.Domain
{
    public enum TrainState
    {
        /// <summary>Travelling; comes back at <see cref="Train.ArrivesAtUnixMs"/>.</summary>
        Away,
        /// <summary>Waiting at the station until the player sends it off (no time limit).</summary>
        AtStation,
    }

    public enum CrateFillResult
    {
        Filled,
        NotAtStation,
        NoSuchCrate,
        AlreadyFilled,
        MissingItems,
    }

    /// <summary>One crate the train asks for: N of an item, paying coins and XP.</summary>
    public sealed class TrainCrate
    {
        public TrainCrate(string itemId, int amount, long rewardCoins, long rewardXp, bool filled = false)
        {
            ItemId = itemId;
            Amount = Math.Max(1, amount);
            RewardCoins = Math.Max(0, rewardCoins);
            RewardXp = Math.Max(0, rewardXp);
            Filled = filled;
        }

        public string ItemId { get; }
        public int Amount { get; }
        public long RewardCoins { get; }
        public long RewardXp { get; }
        public bool Filled { get; internal set; }
    }

    /// <summary>
    /// The town's train. It arrives with a set of crates, waits at the station while the player fills them, and
    /// travels for a while after being sent off. Times are UTC timestamps, so it also travels while the app is closed.
    /// A new train starts "away, arriving at 0", i.e. it is there as soon as a station exists.
    /// </summary>
    public sealed class Train
    {
        private readonly List<TrainCrate> _crates = new List<TrainCrate>();

        public TrainState State { get; private set; } = TrainState.Away;
        public long ArrivesAtUnixMs { get; private set; }
        public IReadOnlyList<TrainCrate> Crates => _crates;
        public bool IsAtStation => State == TrainState.AtStation;

        public int FilledCount
        {
            get
            {
                int n = 0;
                foreach (var c in _crates)
                    if (c.Filled) n++;
                return n;
            }
        }

        public bool AllFilled => _crates.Count > 0 && FilledCount == _crates.Count;

        public long RemainingMs(long nowUnixMs) => State == TrainState.Away ? Math.Max(0, ArrivesAtUnixMs - nowUnixMs) : 0;

        /// <summary>If the train is due, it arrives with the crates from <paramref name="makeCrates"/>. True when it just arrived.</summary>
        public bool UpdateArrival(long nowUnixMs, Func<IReadOnlyList<TrainCrate>> makeCrates)
        {
            if (State != TrainState.Away || nowUnixMs < ArrivesAtUnixMs) return false;
            _crates.Clear();
            _crates.AddRange(makeCrates());
            State = TrainState.AtStation;
            return true;
        }

        /// <summary>Takes the crate's items from the inventory. Rewards are paid by the caller.</summary>
        public CrateFillResult TryFill(int index, Inventory inventory)
        {
            if (State != TrainState.AtStation) return CrateFillResult.NotAtStation;
            if (index < 0 || index >= _crates.Count) return CrateFillResult.NoSuchCrate;
            var crate = _crates[index];
            if (crate.Filled) return CrateFillResult.AlreadyFilled;
            if (!inventory.TryRemove(crate.ItemId, crate.Amount)) return CrateFillResult.MissingItems;
            crate.Filled = true;
            return CrateFillResult.Filled;
        }

        /// <summary>Sends the train away (filled or not). It comes back after <paramref name="travelMs"/> with new crates.</summary>
        public bool SendOff(long nowUnixMs, long travelMs)
        {
            if (State != TrainState.AtStation) return false;
            _crates.Clear();
            State = TrainState.Away;
            ArrivesAtUnixMs = nowUnixMs + Math.Max(0, travelMs);
            return true;
        }

        /// <summary>Brings a travelling train back now (e.g. after a rewarded ad). It arrives on the next update.</summary>
        public bool ArriveNow(long nowUnixMs)
        {
            if (State != TrainState.Away || ArrivesAtUnixMs <= nowUnixMs) return false;
            ArrivesAtUnixMs = nowUnixMs;
            return true;
        }

        public void Restore(TrainState state, long arrivesAtUnixMs, IEnumerable<TrainCrate> crates)
        {
            State = state;
            ArrivesAtUnixMs = arrivesAtUnixMs;
            _crates.Clear();
            if (crates != null) _crates.AddRange(crates);
            if (State == TrainState.AtStation && _crates.Count == 0) State = TrainState.Away; // nothing to fill: let it come back fresh
        }
    }

    /// <summary>Builds a train's crates from what the player can currently make (same candidates as orders).</summary>
    public static class TrainCrateGenerator
    {
        public static List<TrainCrate> Generate(IReadOnlyList<OrderCandidate> candidates, int count, double coinMultiplier, int xpDivisor, Random random)
        {
            var crates = new List<TrainCrate>();
            if (candidates == null || candidates.Count == 0) return crates;
            for (int i = 0; i < count; i++)
            {
                var c = candidates[random.Next(candidates.Count)];
                // Cheap raw goods come in bigger batches than valuable products.
                int amount = c.UnitValue <= 10 ? random.Next(3, 7) : c.UnitValue <= 40 ? random.Next(2, 4) : random.Next(1, 3);
                long value = (long)c.UnitValue * amount;
                long coins = (long)Math.Ceiling(value * coinMultiplier);
                long xp = Math.Max(1, value / Math.Max(1, xpDivisor));
                crates.Add(new TrainCrate(c.ItemId, amount, coins, xp));
            }

            return crates;
        }

        /// <summary>Extra coins for sending off a completely filled train: a share of all crate rewards.</summary>
        public static long FullTrainBonus(IReadOnlyList<TrainCrate> crates, double fraction)
        {
            long sum = 0;
            foreach (var c in crates) sum += c.RewardCoins;
            return (long)Math.Round(sum * Math.Max(0, fraction), MidpointRounding.AwayFromZero);
        }
    }
}
