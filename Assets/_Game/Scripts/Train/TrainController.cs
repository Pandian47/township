using System;
using System.Collections.Generic;
using TownBuilder.Domain;
using UnityEngine;

namespace TownBuilder
{
    /// <summary>
    /// The train in the Town scene: arrives at the (finished) Train Station with crates, the player fills crates for coins
    /// and XP, then sends the train off; it returns after a travel time (also while the app is closed). The station sprite
    /// switches to "train at the platform" while the train is there. Rules live in <see cref="Train"/>.
    /// </summary>
    public sealed class TrainController : MonoBehaviour
    {
        [SerializeField] private TownBuildingController buildings;
        [SerializeField] private float refreshInterval = 0.25f;

        private Town _town;
        private Train _train;
        private Inventory _inventory;
        private Wallet _wallet;
        private PlayerProgress _progress;
        private GameContent _content;
        private IClock _clock;
        private Func<string, bool> _isBuilt;
        private Func<List<OrderCandidate>> _candidates;
        private readonly System.Random _random = new System.Random();
        private float _nextRefresh;
        private string _spriteStationId;
        private bool _spriteShowsTrain;

        public event Action<string> Notification;
        public event Action Modified;
        public event Action Arrived;
        public event Action<int> CrateFilled;
        /// <summary>The train left (true when it was completely filled).</summary>
        public event Action<bool> Departed;
        public event Action Failed;
        /// <summary>Coins/XP earned at the station, for world effects.</summary>
        public event Action<WorldReward> Rewarded;

        public Train Train => _train;
        public long NowUnixMs => _clock.UtcNowUnixMs;

        public void Initialize(Town town, Train train, Inventory inventory, Wallet wallet, PlayerProgress progress, GameContent content,
            IClock clock, Func<string, bool> isBuilt, Func<List<OrderCandidate>> candidates)
        {
            _town = town;
            _train = train;
            _inventory = inventory;
            _wallet = wallet;
            _progress = progress;
            _content = content;
            _clock = clock;
            _isBuilt = isBuilt;
            _candidates = candidates;
            Tick();
        }

        // ------------------------------------------------------------------ Queries

        /// <summary>The finished Train Station's instance id, or null if there is none (yet).</summary>
        public string StationId
        {
            get
            {
                if (_town == null) return null;
                foreach (var b in _town.Buildings)
                    if (b.DefinitionId == _content.TrainStationId && (_isBuilt == null || _isBuilt(b.InstanceId))) return b.InstanceId;
                return null;
            }
        }

        public bool HasStation => StationId != null;

        public bool IsStation(string instanceId) => instanceId != null && instanceId == StationId;

        public long RemainingMs => _train.RemainingMs(NowUnixMs);

        public bool HasItemsFor(int index) =>
            index >= 0 && index < _train.Crates.Count && _inventory.Count(_train.Crates[index].ItemId) >= _train.Crates[index].Amount;

        public long FullTrainBonus => TrainCrateGenerator.FullTrainBonus(_train.Crates, _content.TrainFullBonusFraction);

        // ------------------------------------------------------------------ Commands

        public CrateFillResult Fill(int index)
        {
            var result = _train.TryFill(index, _inventory);
            if (result == CrateFillResult.MissingItems)
            {
                var crate = _train.Crates[index];
                Notification?.Invoke($"You need {crate.Amount} {_content.ItemName(crate.ItemId)} for this crate.");
                Failed?.Invoke();
                return result;
            }

            if (result != CrateFillResult.Filled) return result;

            var filled = _train.Crates[index];
            _wallet.AddCoins(filled.RewardCoins);
            if (filled.RewardXp > 0) _progress.AddXp(filled.RewardXp);
            Notification?.Invoke(_train.AllFilled
                ? $"All crates loaded! Send the train for a bonus of {FullTrainBonus:N0} coins."
                : $"Crate loaded   +{filled.RewardCoins:N0} coins   +{filled.RewardXp} XP");
            CrateFilled?.Invoke(index);
            var station = StationId;
            if (station != null) Rewarded?.Invoke(new WorldReward(station, null, (int)filled.RewardXp, (int)filled.RewardCoins));
            Modified?.Invoke();
            return result;
        }

        /// <summary>Sends the train away. A completely filled train pays the bonus.</summary>
        public bool SendOff()
        {
            if (!_train.IsAtStation) return false;
            bool full = _train.AllFilled;
            long bonus = full ? FullTrainBonus : 0;
            _train.SendOff(NowUnixMs, _content.TrainTravelSeconds * 1000L);
            if (bonus > 0)
            {
                _wallet.AddCoins(bonus);
                var station = StationId;
                if (station != null) Rewarded?.Invoke(new WorldReward(station, null, 0, (int)bonus));
            }

            Notification?.Invoke(full
                ? $"The train is off!   Bonus +{bonus:N0} coins. Back in {FieldPanel.FormatDuration(_content.TrainTravelSeconds * 1000L)}."
                : $"The train is off. Back in {FieldPanel.FormatDuration(_content.TrainTravelSeconds * 1000L)}.");
            Departed?.Invoke(full);
            RefreshSprite();
            Modified?.Invoke();
            return true;
        }

        /// <summary>Brings the train back now (call after a rewarded ad was watched).</summary>
        public bool CallNow()
        {
            if (!_train.ArriveNow(NowUnixMs)) return false;
            Tick();
            Modified?.Invoke();
            return true;
        }

        // ------------------------------------------------------------------ Internals

        private void Update()
        {
            if (_train == null || Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + refreshInterval;
            Tick();
        }

        private void Tick()
        {
            if (!HasStation)
            {
                RefreshSprite();
                return;
            }

            if (_train.UpdateArrival(NowUnixMs, MakeCrates))
            {
                Notification?.Invoke("The train has arrived! Tap the Train Station to load the crates.");
                Arrived?.Invoke();
                Modified?.Invoke();
            }

            RefreshSprite();
        }

        private IReadOnlyList<TrainCrate> MakeCrates() =>
            TrainCrateGenerator.Generate(_candidates(), _content.TrainCrates, _content.TrainCoinMultiplier, _content.TrainXpDivisor, _random);

        /// <summary>Station sprite: with the train at the platform while it's here, empty platform otherwise.</summary>
        private void RefreshSprite()
        {
            string station = StationId;
            bool showTrain = station != null && _train.IsAtStation && _content.TrainAtStationSprite != null;
            if (station == _spriteStationId && showTrain == _spriteShowsTrain) return;
            _spriteStationId = station;
            _spriteShowsTrain = showTrain;
            if (station == null || !buildings.TryGetView(station, out var view) || view.Definition == null) return;
            view.SpriteRenderer.sprite = showTrain ? _content.TrainAtStationSprite : view.Definition.Sprite;
        }
    }
}
