using System;
using System.Collections.Generic;
using System.Linq;
using TownBuilder.Domain;
using UnityEngine;

namespace TownBuilder
{
    /// <summary>
    /// Town happiness in the Town scene. Homes (Residential buildings) collect coins over time, faster when they are
    /// happy; decorations near a home make it happy (<see cref="TownHappiness"/>). A coin bubble bobs above a home with
    /// coins waiting; tapping the home collects them. Rules live in <see cref="TownHappiness"/> and <see cref="HomeCoins"/>.
    /// </summary>
    public sealed class HappinessController : MonoBehaviour
    {
        [SerializeField] private TownBuildingController buildings;
        [SerializeField] private PlacementSystem placement;
        [SerializeField] private Sprite coinIcon;
        [SerializeField] private float bubbleScale = 0.55f;
        [SerializeField] private float refreshInterval = 1f;

        private readonly Dictionary<string, SpriteRenderer> _bubbles = new Dictionary<string, SpriteRenderer>();
        private Town _town;
        private HomeCoins _coins;
        private Wallet _wallet;
        private GameContent _content;
        private IClock _clock;
        private Func<string, bool> _isBuilt;
        private float _nextRefresh;

        public event Action Modified;
        /// <summary>Coins collected from a home (home id, coins).</summary>
        public event Action<string, int> Collected;
        public event Action<WorldReward> Rewarded;

        public HomeCoins Coins => _coins;
        public long NowUnixMs => _clock.UtcNowUnixMs;
        public int Radius => _content.HappinessRadius;
        public double MaxHours => _content.HomeCoinsMaxHours;

        public void Initialize(Town town, HomeCoins coins, Wallet wallet, GameContent content, IClock clock, Func<string, bool> isBuilt)
        {
            _town = town;
            _coins = coins;
            _wallet = wallet;
            _content = content;
            _clock = clock;
            _isBuilt = isBuilt;
            _town.BuildingRemoved += OnBuildingRemoved;
            placement.AddTapInterceptor(OnBuildingTapped);
            Tick();
        }

        private void OnDestroy()
        {
            if (_town != null) _town.BuildingRemoved -= OnBuildingRemoved;
        }

        // ------------------------------------------------------------------ Queries

        public bool IsHome(string instanceId, out BuildingDefinition definition)
        {
            definition = null;
            return _town != null && _town.TryGet(instanceId, out var b) && _content.TryGetBuilding(b.DefinitionId, out definition)
                   && definition.Category == BuildingCategory.Residential && (_isBuilt == null || _isBuilt(instanceId));
        }

        /// <summary>A home's happiness, 0-100.</summary>
        public int Happiness(string homeId)
        {
            if (!_town.TryGet(homeId, out var home) || !_content.TryGetBuilding(home.DefinitionId, out var def)) return 0;
            return TownHappiness.ForHome(home.Origin, home.Size, Sources(), Radius);
        }

        /// <summary>Average happiness of all homes (0 when there are none).</summary>
        public int AverageHappiness
        {
            get
            {
                var homes = Homes().ToList();
                return homes.Count == 0 ? 0 : (int)Math.Round(homes.Average(Happiness));
            }
        }

        public double CoinsPerHour(string homeId) => TownHappiness.CoinsPerHour(_content.HomeCoinsPerHour, Happiness(homeId));

        public int Pending(string homeId) => _coins.Pending(homeId, NowUnixMs, CoinsPerHour(homeId), MaxHours);

        // ------------------------------------------------------------------ Commands

        /// <summary>Empties a home's coins into the wallet. Returns the coins collected.</summary>
        public int Collect(string homeId)
        {
            if (!IsHome(homeId, out _)) return 0;
            int coins = _coins.Collect(homeId, NowUnixMs, CoinsPerHour(homeId), MaxHours);
            if (coins <= 0) return 0;
            _wallet.AddCoins(coins);
            Rewarded?.Invoke(new WorldReward(homeId, null, 0, coins));
            Collected?.Invoke(homeId, coins);
            UpdateBubble(homeId, false);
            Modified?.Invoke();
            return coins;
        }

        // ------------------------------------------------------------------ Internals

        private IEnumerable<string> Homes() =>
            _town.Buildings.Where(b => IsHome(b.InstanceId, out _)).Select(b => b.InstanceId);

        private List<HappinessSource> Sources()
        {
            var list = new List<HappinessSource>();
            foreach (var b in _town.Buildings)
                if (_content.TryGetBuilding(b.DefinitionId, out var d) && d.Happiness > 0)
                    list.Add(new HappinessSource(b.Origin, b.Size, d.Happiness));
            return list;
        }

        private bool OnBuildingTapped(PlacedBuilding building)
        {
            if (!IsHome(building.InstanceId, out _) || Pending(building.InstanceId) < _content.HomeCoinsBubbleMin) return false;
            Collect(building.InstanceId);
            return true;
        }

        private void OnBuildingRemoved(PlacedBuilding building)
        {
            if (_coins.Forget(building.InstanceId)) Modified?.Invoke();
            if (_bubbles.TryGetValue(building.InstanceId, out var bubble))
            {
                if (bubble != null) Destroy(bubble.gameObject);
                _bubbles.Remove(building.InstanceId);
            }
        }

        private void Update()
        {
            if (_coins == null || Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + refreshInterval;
            Tick();
        }

        private void Tick()
        {
            bool changed = false;
            foreach (var home in Homes().ToList())
            {
                changed |= _coins.Track(home, NowUnixMs);
                UpdateBubble(home, Pending(home) >= _content.HomeCoinsBubbleMin);
            }

            if (changed) Modified?.Invoke();
        }

        public bool BubbleVisible(string homeId) => _bubbles.TryGetValue(homeId, out var b) && b != null && b.enabled;

        private void UpdateBubble(string homeId, bool show)
        {
            _bubbles.TryGetValue(homeId, out var bubble);
            if (!show)
            {
                if (bubble != null) bubble.enabled = false;
                return;
            }

            if (coinIcon == null || !buildings.TryGetView(homeId, out var view)) return;
            if (bubble == null || bubble.transform.parent != view.transform)
            {
                if (bubble != null) Destroy(bubble.gameObject);
                var go = new GameObject("CoinBubble");
                go.transform.SetParent(view.transform, false);
                bubble = go.AddComponent<SpriteRenderer>();
                bubble.sprite = coinIcon;
                bubble.sortingLayerName = SortingLayers.WorldUI;
                float top = view.SpriteRenderer.bounds.max.y - view.transform.position.y;
                float centre = view.SpriteRenderer.bounds.center.x - view.transform.position.x;
                FloatBob.Place(go.transform, new Vector3(centre, top + 0.25f, 0f));
                go.transform.localScale = Vector3.one * bubbleScale;
                _bubbles[homeId] = bubble;
            }

            bubble.enabled = true;
        }
    }
}
