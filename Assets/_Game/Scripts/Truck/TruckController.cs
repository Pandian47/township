using System;
using System.Collections.Generic;
using System.Linq;
using TownBuilder.Domain;
using UnityEngine;

namespace TownBuilder
{
    /// <summary>
    /// The delivery truck in the Town scene. It parks at the (finished) Truck Depot with mixed loads; the player loads
    /// what they can and sends it on a long trip; the coins, XP and building materials arrive when it comes back (also
    /// while the app was closed). A truck badge bobs above the depot while the truck is parked there.
    /// Rules live in <see cref="DeliveryTruck"/>.
    /// </summary>
    public sealed class TruckController : MonoBehaviour
    {
        [SerializeField] private TownBuildingController buildings;
        [SerializeField] private float refreshInterval = 0.5f;
        [Tooltip("Badge above the depot while the truck is parked there.")]
        [SerializeField] private Sprite truckIcon;
        [SerializeField] private float badgeScale = 0.6f;

        private Town _town;
        private DeliveryTruck _truck;
        private Inventory _inventory;
        private Wallet _wallet;
        private PlayerProgress _progress;
        private GameContent _content;
        private IClock _clock;
        private Func<string, bool> _isBuilt;
        private Func<List<OrderCandidate>> _candidates;
        private readonly System.Random _random = new System.Random();
        private float _nextRefresh;
        private SpriteRenderer _badge;

        public event Action<string> Notification;
        public event Action Modified;
        public event Action<int> Loaded;
        /// <summary>The truck left (true when completely loaded).</summary>
        public event Action<bool> Departed;
        /// <summary>The truck came back and paid its cargo.</summary>
        public event Action<TruckPayout> Returned;
        public event Action Failed;
        public event Action<WorldReward> Rewarded;

        public DeliveryTruck Truck => _truck;
        public long NowUnixMs => _clock.UtcNowUnixMs;

        public void Initialize(Town town, DeliveryTruck truck, Inventory inventory, Wallet wallet, PlayerProgress progress, GameContent content,
            IClock clock, Func<string, bool> isBuilt, Func<List<OrderCandidate>> candidates)
        {
            _town = town;
            _truck = truck;
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

        /// <summary>The finished Truck Depot's instance id, or null.</summary>
        public string DepotId
        {
            get
            {
                if (_town == null) return null;
                foreach (var b in _town.Buildings)
                    if (b.DefinitionId == _content.TruckDepotId && (_isBuilt == null || _isBuilt(b.InstanceId))) return b.InstanceId;
                return null;
            }
        }

        public bool HasDepot => DepotId != null;
        public bool IsDepot(string instanceId) => instanceId != null && instanceId == DepotId;
        public long RemainingMs => _truck.RemainingMs(NowUnixMs);
        public bool HasItemsFor(int index) => _truck.HasItemsFor(index, _inventory);
        public long FullBonus => _truck.FullBonus(_content.TruckFullBonusFraction);
        public int GemCostToCallBack => _truck.State == TruckState.Away && RemainingMs > 0 ? TimeSkip.GemCost(RemainingMs) : 0;
        public bool HasGems(long amount) => _wallet.Gems >= amount;
        public bool BadgeVisible => _badge != null && _badge.enabled;

        // ------------------------------------------------------------------ Commands

        public TruckLoadResult Load(int index)
        {
            var result = _truck.TryLoad(index, _inventory);
            if (result == TruckLoadResult.MissingItems)
            {
                var missing = _truck.Loads[index].Lines.Where(l => _inventory.Count(l.ItemId) < l.Amount)
                    .Select(l => $"{l.Amount - _inventory.Count(l.ItemId)} more {_content.ItemName(l.ItemId)}");
                Notification?.Invoke($"You need {string.Join(" and ", missing)} for this load.");
                Failed?.Invoke();
                return result;
            }

            if (result != TruckLoadResult.Loaded) return result;
            Notification?.Invoke(_truck.AllLoaded
                ? $"The truck is full! Send it for a bonus of {FullBonus:N0} coins."
                : $"Load {index + 1} is on the truck. Send it when you're ready, or fill more loads first.");
            Loaded?.Invoke(index);
            Modified?.Invoke();
            return result;
        }

        public bool SendOff()
        {
            if (!_truck.CanSend)
            {
                Notification?.Invoke("Put at least one load on the truck first.");
                Failed?.Invoke();
                return false;
            }

            bool full = _truck.AllLoaded;
            long tripMs = _content.TruckTripSeconds * 1000L;
            _truck.SendOff(NowUnixMs, tripMs, _content.TruckFullBonusFraction);
            Notification?.Invoke($"The truck is off! It brings back {_truck.Cargo.Coins:N0} coins in {FieldPanel.FormatDuration(tripMs)}.");
            Departed?.Invoke(full);
            RefreshBadge();
            Modified?.Invoke();
            return true;
        }

        /// <summary>Brings the truck back now (after a rewarded ad).</summary>
        public bool CallBack()
        {
            if (!_truck.ReturnNow(NowUnixMs)) return false;
            Tick();
            Modified?.Invoke();
            return true;
        }

        /// <summary>Brings the truck back now for gems (1 per minute left).</summary>
        public bool CallBackWithGems()
        {
            int cost = GemCostToCallBack;
            if (cost <= 0) return false;
            if (!_wallet.TrySpendGems(cost))
            {
                Notification?.Invoke($"You need {cost} gems.");
                Failed?.Invoke();
                return false;
            }

            return CallBack();
        }

        // ------------------------------------------------------------------ Internals

        private void Update()
        {
            if (_truck == null || Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + refreshInterval;
            Tick();
        }

        private void Tick()
        {
            if (!HasDepot)
            {
                RefreshBadge();
                return;
            }

            var payout = _truck.UpdateReturn(NowUnixMs, MakeLoads);
            if (payout != null)
            {
                if (!payout.IsEmpty) Pay(payout);
                else Notification?.Invoke("The delivery truck is at the depot! Tap the Truck Depot to load it.");
                Modified?.Invoke();
            }

            RefreshBadge();
        }

        private void Pay(TruckPayout payout)
        {
            long coins = payout.Coins;
            var gains = new List<ItemGain>();
            var parts = new List<string>();
            foreach (var pair in payout.Materials)
            {
                if (!_content.TryGetItem(pair.Key, out var material)) continue;
                if (_inventory.TryAdd(pair.Key, pair.Value))
                {
                    gains.Add(new ItemGain(material, pair.Value));
                    parts.Add($"+{pair.Value} {material.DisplayName}");
                }
                else
                {
                    coins += Math.Max(1, material.SellPrice) * pair.Value * 2L; // barn full: paid as coins instead
                }
            }

            if (coins > 0) _wallet.AddCoins(coins);
            if (payout.Xp > 0) _progress.AddXp(payout.Xp);
            Notification?.Invoke($"The truck is back!   +{coins:N0} coins   +{payout.Xp} XP   {string.Join("   ", parts)}".TrimEnd());
            var depot = DepotId;
            if (depot != null) Rewarded?.Invoke(new WorldReward(depot, gains, (int)payout.Xp, (int)coins));
            Returned?.Invoke(payout);
        }

        private IReadOnlyList<TruckLoad> MakeLoads()
        {
            var materials = _content.Materials.Where(m => m != null && !string.IsNullOrEmpty(m.Id)).Select(m => m.Id).ToList();
            return TruckLoadGenerator.Generate(_candidates(), _content.TruckLoads, _content.TruckLinesPerLoad, _content.TruckCoinMultiplier,
                _content.TruckXpDivisor, _random, materials);
        }

        private void RefreshBadge()
        {
            string depot = DepotId;
            bool show = depot != null && _truck.IsAtDepot && truckIcon != null;
            if (!show)
            {
                if (_badge != null) _badge.enabled = false;
                return;
            }

            if (!buildings.TryGetView(depot, out var view)) return;
            if (_badge == null || _badge.transform.parent != view.transform)
            {
                if (_badge != null) Destroy(_badge.gameObject);
                var go = new GameObject("TruckBadge");
                go.transform.SetParent(view.transform, false);
                _badge = go.AddComponent<SpriteRenderer>();
                _badge.sprite = truckIcon;
                _badge.sortingLayerName = SortingLayers.WorldUI;
                float top = view.SpriteRenderer.bounds.max.y - view.transform.position.y;
                float centre = view.SpriteRenderer.bounds.center.x - view.transform.position.x;
                FloatBob.Place(go.transform, new Vector3(centre, top + 0.35f, 0f));
                go.transform.localScale = Vector3.one * badgeScale;
            }

            _badge.enabled = true;
        }
    }
}
