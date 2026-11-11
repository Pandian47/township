using System;
using System.Linq;
using TownBuilder.Domain;

namespace TownBuilder
{
    /// <summary>
    /// Collections in the Town scene: every harvest, factory collect and delivered order may find a rare item;
    /// completing a set pays coins and gems. Plain C#, owned by TownSceneController.
    /// </summary>
    public sealed class CollectionService : IDisposable
    {
        private readonly CollectionBook _book;
        private readonly GameContent _content;
        private readonly Wallet _wallet;
        private readonly Random _random;
        private readonly Action _unsubscribe;

        public CollectionService(CollectionBook book, GameContent content, Wallet wallet, FarmingController farming,
            ProductionController production, OrderService orders, Random random = null)
        {
            _book = book;
            _content = content;
            _wallet = wallet;
            _random = random ?? new Random();

            Action<WorldReward> harvested = _ => Roll(AchievementKind.HarvestCrops);
            Action<WorldReward> collected = _ => Roll(AchievementKind.MakeProducts);
            Action<int> delivered = _ => Roll(AchievementKind.DeliverOrders);
            farming.Rewarded += harvested;
            production.Rewarded += collected;
            orders.Delivered += delivered;
            _unsubscribe = () =>
            {
                farming.Rewarded -= harvested;
                production.Rewarded -= collected;
                orders.Delivered -= delivered;
            };
        }

        public event Action<string> Notification;
        public event Action Modified;
        /// <summary>An item was found (collection id, item id).</summary>
        public event Action<string, string> Found;
        /// <summary>A set was completed (collection id).</summary>
        public event Action<string> Completed;

        public CollectionBook Book => _book;
        public GameContent Content => _content;

        /// <summary>Uses one of each item of a set and pays its reward.</summary>
        public bool Complete(string setId)
        {
            if (!_content.TryGetCollection(setId, out var def) || !_book.TryComplete(setId)) return false;
            if (def.RewardCoins > 0) _wallet.AddCoins(def.RewardCoins);
            if (def.RewardGems > 0) _wallet.AddGems(def.RewardGems);
            Notification?.Invoke($"{def.DisplayName} complete!   +{def.RewardText()}");
            Completed?.Invoke(setId);
            Modified?.Invoke();
            return true;
        }

        /// <summary>Developer mode: finds an item (one the player doesn't have yet when possible).</summary>
        public string DevFindItem()
        {
            foreach (var set in _book.Sets)
            {
                var missing = set.ItemIds.FirstOrDefault(i => _book.Count(i) == 0);
                if (missing == null) continue;
                Give(set.Id, missing);
                return missing;
            }

            var any = _book.Sets.FirstOrDefault();
            if (any == null) return null;
            Give(any.Id, any.ItemIds[0]);
            return any.ItemIds[0];
        }

        public void Dispose() => _unsubscribe();

        private void Roll(AchievementKind source)
        {
            string item = _book.Roll(source, _random);
            if (item == null) return;
            var set = _book.Sets.First(s => s.ItemIds.Contains(item));
            Announce(set.Id, item);
        }

        private void Give(string setId, string itemId)
        {
            _book.Add(itemId);
            Announce(setId, itemId);
        }

        private void Announce(string setId, string itemId)
        {
            if (_content.TryGetCollection(setId, out var def) && def.TryGetItem(itemId, out var item))
            {
                string complete = _book.CanComplete(setId) ? $"   {def.DisplayName} is complete: claim it in Collections!" : "";
                Notification?.Invoke($"Rare find: {item.displayName}! ({_book.DistinctOwned(setId)}/{def.Items.Count} {def.DisplayName}){complete}");
            }

            Found?.Invoke(setId, itemId);
            Modified?.Invoke();
        }
    }
}
