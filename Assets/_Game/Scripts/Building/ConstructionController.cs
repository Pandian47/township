using System;
using System.Collections.Generic;
using TownBuilder.Domain;
using UnityEngine;

namespace TownBuilder
{
    /// <summary>
    /// Construction timers: buildings with a build time start as construction sites when placed, show a hammer
    /// badge, don't work (no production, no storage bonus) until finished, and grant XP when they complete.
    /// Sites can be finished early with a rewarded ad (via the UI) or gems.
    /// </summary>
    public sealed class ConstructionController : MonoBehaviour
    {
        [SerializeField] private TownBuildingController buildings;
        [SerializeField] private PlacementSystem placement;
        [SerializeField] private Sprite constructionIcon;
        [SerializeField] private float iconScale = 0.5f;
        [SerializeField] private float refreshInterval = 0.25f;

        private readonly Dictionary<string, SpriteRenderer> _icons = new Dictionary<string, SpriteRenderer>();
        private Town _town;
        private ConstructionSites _sites;
        private Wallet _wallet;
        private PlayerProgress _progress;
        private GameContent _content;
        private IClock _clock;
        private float _nextRefresh;

        public event Action<string> Notification;
        public event Action Modified;

        /// <summary>Raised when a building finishes construction (storage, production... become active).</summary>
        public event Action<string> Completed;
        public event Action Failed;
        /// <summary>Construction finished: the XP it earned, for world effects.</summary>
        public event Action<WorldReward> Rewarded;

        public ConstructionSites Sites => _sites;
        public long NowUnixMs => _clock.UtcNowUnixMs;

        public void Initialize(Town town, ConstructionSites sites, Wallet wallet, PlayerProgress progress, GameContent content, IClock clock)
        {
            _town = town;
            _sites = sites;
            _wallet = wallet;
            _progress = progress;
            _content = content;
            _clock = clock;

            placement.BuildingPlaced += OnBuildingPlaced;
            _town.BuildingRemoved += OnBuildingRemoved;
            _sites.Changed += RefreshIcon;
            foreach (var id in _sites.Sites.Keys) RefreshIcon(id);
            CompleteFinished();
        }

        private void OnDestroy()
        {
            if (placement != null) placement.BuildingPlaced -= OnBuildingPlaced;
            if (_town != null) _town.BuildingRemoved -= OnBuildingRemoved;
            if (_sites != null) _sites.Changed -= RefreshIcon;
        }

        public bool IsBuilt(string instanceId) => _sites == null || !_sites.IsUnderConstruction(instanceId);

        public bool TryGetSite(string instanceId, out ConstructionSite site) => _sites.TryGet(instanceId, out site);

        public int GemCostToFinish(string instanceId) =>
            _sites.TryGet(instanceId, out var site) ? ConstructionSites.GemCostToFinish(site.RemainingMs(NowUnixMs)) : 0;

        /// <summary>Finishes construction now (call after a rewarded ad was watched).</summary>
        public bool FinishNow(string instanceId)
        {
            if (!_sites.FinishNow(instanceId, NowUnixMs)) return false;
            CompleteFinished();
            return true;
        }

        public bool FinishWithGems(string instanceId)
        {
            int cost = GemCostToFinish(instanceId);
            if (cost <= 0) return false;
            if (!_wallet.TrySpendGems(cost))
            {
                Notification?.Invoke($"You need {cost} gems.");
                Failed?.Invoke();
                return false;
            }

            return FinishNow(instanceId);
        }

        private void OnBuildingPlaced(PlacedBuilding building)
        {
            if (!_content.TryGetBuilding(building.DefinitionId, out var def) || def.BuildDurationMs <= 0) return;
            _sites.Start(building.InstanceId, NowUnixMs, def.BuildDurationMs);
            Notification?.Invoke($"Building {def.DisplayName}... ready in {FieldPanel.FormatDuration(def.BuildDurationMs)}");
            Modified?.Invoke();
        }

        private void OnBuildingRemoved(PlacedBuilding building)
        {
            _sites.Remove(building.InstanceId);
            if (_icons.TryGetValue(building.InstanceId, out var icon))
            {
                if (icon != null) Destroy(icon.gameObject);
                _icons.Remove(building.InstanceId);
            }
        }

        private void Update()
        {
            if (_sites == null || _sites.Sites.Count == 0 || Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + refreshInterval;
            CompleteFinished();
        }

        private void CompleteFinished()
        {
            var done = _sites.TakeFinished(NowUnixMs);
            if (done.Count == 0) return;

            foreach (var id in done)
            {
                if (!_town.TryGet(id, out var building) || !_content.TryGetBuilding(building.DefinitionId, out var def)) continue;
                if (def.BuildXp > 0) _progress.AddXp(def.BuildXp);
                Notification?.Invoke(def.BuildXp > 0 ? $"{def.DisplayName} is ready!   +{def.BuildXp} XP" : $"{def.DisplayName} is ready!");
                Completed?.Invoke(id);
                Rewarded?.Invoke(new WorldReward(id, null, def.BuildXp));
            }

            Modified?.Invoke();
        }

        private void RefreshIcon(string instanceId)
        {
            bool underConstruction = _sites.IsUnderConstruction(instanceId);
            _icons.TryGetValue(instanceId, out var icon);

            if (!underConstruction)
            {
                if (icon != null) icon.enabled = false;
                return;
            }

            if (icon == null)
            {
                if (!buildings.TryGetView(instanceId, out var view)) return;
                var go = new GameObject("ConstructionIcon");
                go.transform.SetParent(view.transform, false);
                icon = go.AddComponent<SpriteRenderer>();
                icon.sprite = constructionIcon;
                icon.sortingLayerName = SortingLayers.WorldUI;
                _icons[instanceId] = icon;

                float top = view.SpriteRenderer.bounds.max.y - view.transform.position.y;
                float centerX = view.SpriteRenderer.bounds.center.x - view.transform.position.x;
                go.transform.localPosition = new Vector3(centerX, top + 0.3f, 0f);
                go.transform.localScale = Vector3.one * iconScale;
            }

            icon.enabled = true;
        }
    }
}
