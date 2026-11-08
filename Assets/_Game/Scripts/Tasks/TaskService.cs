using System;
using System.Collections.Generic;
using System.Linq;
using TownBuilder.Domain;

namespace TownBuilder
{
    /// <summary>
    /// Town tasks in the Town scene: turns game events into task progress, pays rewards on claim and keeps the board
    /// filled (also after a level-up unlocks new tasks). Plain C#, owned by TownSceneController.
    /// </summary>
    public sealed class TaskService : IDisposable
    {
        private readonly TaskBoard _board;
        private readonly GameContent _content;
        private readonly Wallet _wallet;
        private readonly Inventory _inventory;
        private readonly PlayerProgress _progress;
        private readonly Town _town;
        private readonly List<TaskSpec> _catalog;
        private readonly Random _random;
        private readonly Action _unsubscribe;

        public TaskService(TaskBoard board, GameContent content, Wallet wallet, Inventory inventory, PlayerProgress progress, Town town,
            FarmingController farming, ProductionController production, OrderService orders, PlacementSystem placement,
            TrainController train = null, int? randomSeed = null)
        {
            _board = board;
            _content = content;
            _wallet = wallet;
            _inventory = inventory;
            _progress = progress;
            _town = town;
            _random = randomSeed.HasValue ? new Random(randomSeed.Value) : new Random();
            _catalog = content.Tasks.Where(t => t != null && !string.IsNullOrEmpty(t.Id)).Select(t => t.ToSpec()).ToList();

            Action<WorldReward> harvested = r => RecordItems(TaskKind.Harvest, r);
            Action<WorldReward> collected = r => RecordItems(TaskKind.Collect, r);
            Action<int> delivered = _ => _board.Record(TaskKind.Deliver, null, 1);
            Action<PlacedBuilding> placed = b => _board.Record(TaskKind.Build, b.DefinitionId, 1);
            Action<int> leveledUp = _ => Refill();
            Action<int> crateLoaded = _ => _board.Record(TaskKind.LoadCrates, null, 1);
            Action<int> completed = OnTaskCompleted;
            Action<int> changed = _ => Modified?.Invoke();

            farming.Rewarded += harvested;
            production.Rewarded += collected;
            orders.Delivered += delivered;
            placement.BuildingPlaced += placed;
            if (train != null) train.CrateFilled += crateLoaded;
            progress.LeveledUp += leveledUp;
            board.TaskCompleted += completed;
            board.Changed += changed;

            _unsubscribe = () =>
            {
                farming.Rewarded -= harvested;
                production.Rewarded -= collected;
                orders.Delivered -= delivered;
                placement.BuildingPlaced -= placed;
                if (train != null) train.CrateFilled -= crateLoaded;
                progress.LeveledUp -= leveledUp;
                board.TaskCompleted -= completed;
                board.Changed -= changed;
            };

            Refill();
        }

        /// <summary>Short player-facing feedback ("Task done!").</summary>
        public event Action<string> Notification;
        /// <summary>Task state changed (autosave hook).</summary>
        public event Action Modified;
        /// <summary>A task reached its goal (slot index).</summary>
        public event Action<int> Completed;
        /// <summary>A task's reward was claimed (slot index).</summary>
        public event Action<int> Claimed;

        public TaskBoard Board => _board;

        public TaskDefinition DefinitionAt(int slot)
        {
            var task = _board.Get(slot);
            return task != null && _content.TryGetTask(task.Spec.Id, out var def) ? def : null;
        }

        /// <summary>Pays the reward of a completed task and refills its slot.</summary>
        public bool Claim(int slot)
        {
            var def = DefinitionAt(slot);
            var task = _board.Get(slot);
            if (task == null || !task.IsComplete) return false;

            // Materials go to the barn: don't lose them to a full barn.
            if (def != null && def.RewardMaterialAmount > 0 && !_inventory.CanAdd(def.RewardMaterialAmount))
            {
                Notification?.Invoke($"The barn is full. Make room for {def.RewardMaterialAmount} {def.RewardMaterial.DisplayName} first.");
                return false;
            }

            if (!_board.TryClaim(slot, out _)) return false;

            if (def != null)
            {
                if (def.RewardCoins > 0) _wallet.AddCoins(def.RewardCoins);
                if (def.RewardGems > 0) _wallet.AddGems(def.RewardGems);
                if (def.RewardMaterialAmount > 0) _inventory.TryAdd(def.RewardMaterial.Id, def.RewardMaterialAmount);
                if (def.RewardXp > 0) _progress.AddXp(def.RewardXp); // may level up, which refills too
                Notification?.Invoke($"Task complete!   {def.RewardText()}");
            }

            Claimed?.Invoke(slot);
            Refill();
            Modified?.Invoke();
            return true;
        }

        public void Dispose() => _unsubscribe();

        private void Refill()
        {
            // One-off "Build X" tasks are skipped when the player already owns X.
            _board.Fill(_catalog, _progress.Level, _random,
                spec => spec.Kind == TaskKind.Build && spec.TargetId != null && _town.CountOf(spec.TargetId) > 0);
        }

        private void RecordItems(TaskKind kind, WorldReward reward)
        {
            foreach (var gain in reward.Items)
                if (gain.Item != null && gain.Amount > 0) _board.Record(kind, gain.Item.Id, gain.Amount);
        }

        private void OnTaskCompleted(int slot)
        {
            var def = DefinitionAt(slot);
            if (def != null) Notification?.Invoke($"Task done: {def.Describe()}. Tap Claim!");
            Completed?.Invoke(slot);
        }
    }
}
