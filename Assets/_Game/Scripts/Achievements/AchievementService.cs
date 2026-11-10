using System;
using System.Linq;
using TownBuilder.Domain;

namespace TownBuilder
{
    /// <summary>
    /// Achievements in the Town scene: turns game events into achievement counters, pays gems when a tier is claimed.
    /// Plain C#, owned by TownSceneController.
    /// </summary>
    public sealed class AchievementService : IDisposable
    {
        private readonly AchievementBook _book;
        private readonly GameContent _content;
        private readonly Wallet _wallet;
        private readonly PlayerProgress _progress;
        private readonly Town _town;
        private readonly LandPlots _land;
        private readonly int _startPlots;
        private readonly Action _unsubscribe;

        public AchievementService(AchievementBook book, GameContent content, Wallet wallet, PlayerProgress progress, Town town,
            FarmingController farming, ProductionController production, OrderService orders, PlacementSystem placement,
            TrainController train = null, LandPlots land = null)
        {
            _book = book;
            _content = content;
            _wallet = wallet;
            _progress = progress;
            _town = town;
            _land = land;
            _startPlots = content != null
                ? (content.LandStartMax.x - content.LandStartMin.x + 1) * (content.LandStartMax.y - content.LandStartMin.y + 1)
                : 0;

            // "Highest so far" counters also cover what happened before achievements existed (quietly, no toasts).
            SyncStateCounters();

            Action<WorldReward> harvested = r => _book.Add(AchievementKind.HarvestCrops, r.Items.Sum(i => (long)Math.Max(0, i.Amount)));
            Action<WorldReward> collected = r => _book.Add(AchievementKind.MakeProducts, r.Items.Sum(i => (long)Math.Max(0, i.Amount)));
            Action<int> delivered = _ => _book.Add(AchievementKind.DeliverOrders, 1);
            Action<int> crateLoaded = _ => _book.Add(AchievementKind.LoadCrates, 1);
            Action<PlotCoord> landBought = _ => _book.Add(AchievementKind.BuyLand, 1);
            Action<PlacedBuilding> placed = _ => _book.RaiseTo(AchievementKind.OwnBuildings, CountOwnedBuildings());
            Action<int> leveledUp = level => _book.RaiseTo(AchievementKind.ReachLevel, level);
            Action<string> reached = OnGoalReached;
            Action changed = () => Modified?.Invoke();

            farming.Rewarded += harvested;
            production.Rewarded += collected;
            orders.Delivered += delivered;
            placement.BuildingPlaced += placed;
            if (train != null) train.CrateFilled += crateLoaded;
            if (land != null) land.PlotBought += landBought;
            progress.LeveledUp += leveledUp;
            book.GoalReached += reached;
            book.Changed += changed;

            _unsubscribe = () =>
            {
                farming.Rewarded -= harvested;
                production.Rewarded -= collected;
                orders.Delivered -= delivered;
                placement.BuildingPlaced -= placed;
                if (train != null) train.CrateFilled -= crateLoaded;
                if (land != null) land.PlotBought -= landBought;
                progress.LeveledUp -= leveledUp;
                book.GoalReached -= reached;
                book.Changed -= changed;
            };
        }

        /// <summary>Short player-facing feedback.</summary>
        public event Action<string> Notification;
        /// <summary>Counters or claims changed (autosave hook, panel refresh).</summary>
        public event Action Modified;
        /// <summary>An achievement tier can now be claimed (id).</summary>
        public event Action<string> Ready;
        /// <summary>A tier was claimed (id).</summary>
        public event Action<string> Claimed;

        public AchievementBook Book => _book;
        public GameContent Content => _content;

        /// <summary>Claims the next tier of an achievement and pays its gems.</summary>
        public bool Claim(string id)
        {
            if (!_content.TryGetAchievement(id, out var def)) return false;
            if (!_book.TryClaim(id, out int tier)) return false;
            int gems = def.GemsForTier(tier);
            if (gems > 0) _wallet.AddGems(gems);
            Notification?.Invoke($"{def.DisplayName} (tier {tier + 1}) complete!   +{gems} {(gems == 1 ? "gem" : "gems")}");
            Claimed?.Invoke(id);
            return true;
        }

        public void Dispose() => _unsubscribe();

        private void SyncStateCounters()
        {
            _book.RaiseTo(AchievementKind.ReachLevel, _progress.Level);
            _book.RaiseTo(AchievementKind.OwnBuildings, CountOwnedBuildings());
            if (_land != null) _book.RaiseTo(AchievementKind.BuyLand, Math.Max(0, _land.Owned.Count - _startPlots));
        }

        /// <summary>Buildings the player built: not fields, fruit trees, decorations or fixed landmarks.</summary>
        private int CountOwnedBuildings() =>
            _town.Buildings.Count(b => _content.TryGetBuilding(b.DefinitionId, out var d)
                                       && d.Movable && !d.IsFarmPlot && !d.IsFruitTree && d.Category != BuildingCategory.Decoration);

        private void OnGoalReached(string id)
        {
            if (_content.TryGetAchievement(id, out var def))
                Notification?.Invoke($"Achievement ready: {def.DisplayName}! Tap Goals to claim.");
            Ready?.Invoke(id);
        }
    }
}
