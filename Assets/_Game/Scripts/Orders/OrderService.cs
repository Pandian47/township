using System;
using System.Collections.Generic;
using TownBuilder.Domain;

namespace TownBuilder
{
    /// <summary>
    /// Keeps the order board filled with orders for items the player can currently make (unlocked crops and
    /// products of unlocked recipes), and handles delivering and skipping.
    /// </summary>
    public sealed class OrderService
    {
        private readonly OrderBoard _board;
        private readonly Inventory _inventory;
        private readonly Wallet _wallet;
        private readonly PlayerProgress _progress;
        private readonly GameContent _content;
        private readonly IClock _clock;
        private readonly OrderGenerator _generator = new OrderGenerator();
        private readonly Random _random;

        public OrderService(OrderBoard board, Inventory inventory, Wallet wallet, PlayerProgress progress, GameContent content,
            IClock clock, int? randomSeed = null)
        {
            _board = board;
            _inventory = inventory;
            _wallet = wallet;
            _progress = progress;
            _content = content;
            _clock = clock;
            _random = randomSeed.HasValue ? new Random(randomSeed.Value) : new Random();
            EnsureFilled();
        }

        public event Action<string> Notification;
        public event Action Modified;
        public event Action<int> Delivered;
        public event Action Failed;

        public OrderBoard Board => _board;
        public long NowUnixMs => _clock.UtcNowUnixMs;

        public bool CanDeliver(int slot) => _board.CanDeliver(slot, _inventory, NowUnixMs);

        public DeliverResult Deliver(int slot)
        {
            var order = _board.Get(slot);
            var result = _board.TryDeliver(slot, _inventory, _wallet, _progress, NowUnixMs);
            if (result == DeliverResult.Delivered)
            {
                Notification?.Invoke($"Order delivered!   +{order.RewardCoins:N0} coins   +{order.RewardXp} XP");
                _board.Set(slot, Generate(availableAt: 0));
                Delivered?.Invoke(slot);
                Modified?.Invoke();
            }
            else if (result == DeliverResult.MissingItems)
            {
                Notification?.Invoke("Not enough items for this order yet.");
                Failed?.Invoke();
            }

            return result;
        }

        /// <summary>Discards an order; a new one appears in its slot after the skip cooldown.</summary>
        public void Skip(int slot)
        {
            var order = _board.Get(slot);
            if (order == null || !order.IsAvailable(NowUnixMs)) return;
            _board.Set(slot, Generate(availableAt: NowUnixMs + _content.OrderSkipCooldownMs));
            Modified?.Invoke();
        }

        /// <summary>Fills empty slots (new game, or after content changed).</summary>
        public void EnsureFilled()
        {
            bool changed = false;
            for (int i = 0; i < _board.SlotCount; i++)
            {
                if (_board.Get(i) != null) continue;
                var order = Generate(availableAt: 0);
                if (order == null) break;
                _board.Set(i, order);
                changed = true;
            }

            if (changed) Modified?.Invoke();
        }

        public List<OrderCandidate> Candidates()
        {
            var list = new List<OrderCandidate>();
            foreach (var crop in _content.Crops)
                if (crop != null && _progress.IsUnlocked(crop.UnlockLevel) && crop.SellPrice > 0)
                    list.Add(new OrderCandidate(crop.Id, crop.SellPrice));

            var seen = new HashSet<string>();
            foreach (var recipe in _content.Recipes)
            {
                if (recipe == null || recipe.Output == null || !_progress.IsUnlocked(recipe.UnlockLevel)) continue;
                if (!seen.Add(recipe.Output.Id)) continue;
                list.Add(new OrderCandidate(recipe.Output.Id, Math.Max(1, recipe.Output.SellPrice)));
            }

            return list;
        }

        private Order Generate(long availableAt)
        {
            var candidates = Candidates();
            return candidates.Count == 0 ? null : _generator.Generate(candidates, _progress.Level, _random, availableAt);
        }
    }
}
