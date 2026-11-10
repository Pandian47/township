using System;
using System.Collections.Generic;
using TownBuilder.Domain;
using UnityEngine;

namespace TownBuilder
{
    public enum PlantResult
    {
        Planted,
        NotAField,
        NotEmpty,
        Locked,
        NotEnoughCoins,
    }

    /// <summary>
    /// Farming in the Town scene: plant / grow / harvest on field buildings, fruit trees that regrow by themselves,
    /// crop sprites on top of fields (for trees: the whole tree with its fruit, in place of the plain tree sprite). Rules live in <see cref="FarmPlots"/> and <see cref="Inventory"/>;
    /// this class applies costs, yields and XP and keeps the visuals in sync.
    /// </summary>
    public sealed class FarmingController : MonoBehaviour
    {
        public const float CropSortOffset = 0.001f;

        [SerializeField] private TownBuildingController buildings;
        [SerializeField] private PlacementSystem placement;
        [Tooltip("Material for crop sprites (TownBuilder/SpriteSway: plants move in the wind). Empty = default sprite material.")]
        [SerializeField] private Material cropMaterial;
        [Tooltip("How often growing crops refresh their sprite (seconds).")]
        [SerializeField] private float visualRefreshInterval = 0.25f;

        private readonly Dictionary<string, SpriteRenderer> _cropRenderers = new Dictionary<string, SpriteRenderer>();
        private Town _town;
        private FarmPlots _plots;
        private Inventory _inventory;
        private Wallet _wallet;
        private PlayerProgress _progress;
        private GameContent _content;
        private IClock _clock;
        private float _nextRefresh;

        /// <summary>Short player-facing feedback ("+2 Wheat", "Barn is full").</summary>
        public event Action<string> Notification;

        /// <summary>Farm or inventory state changed because of a player action (autosave hook).</summary>
        public event Action Modified;

        public event Action<string> Planted;
        public event Action<string> Harvested;
        /// <summary>A harvest or plant attempt failed (barn full, not enough coins...).</summary>
        public event Action Failed;
        /// <summary>A harvest succeeded: what it earned, for world effects.</summary>
        public event Action<WorldReward> Rewarded;

        public FarmPlots Plots => _plots;
        public Inventory Inventory => _inventory;
        public long NowUnixMs => _clock.UtcNowUnixMs;

        public void Initialize(Town town, FarmPlots plots, Inventory inventory, Wallet wallet, PlayerProgress progress,
            GameContent content, IClock clock)
        {
            _town = town;
            _plots = plots;
            _inventory = inventory;
            _wallet = wallet;
            _progress = progress;
            _content = content;
            _clock = clock;

            _town.BuildingAdded += OnBuildingAdded;
            _town.BuildingRemoved += OnBuildingRemoved;
            _plots.PlotChanged += RefreshPlot;
            placement.AddTapInterceptor(OnBuildingTapped);

            foreach (var b in _town.Buildings) OnBuildingAdded(b);
        }

        private void OnDestroy()
        {
            if (_town != null)
            {
                _town.BuildingAdded -= OnBuildingAdded;
                _town.BuildingRemoved -= OnBuildingRemoved;
            }

            if (_plots != null) _plots.PlotChanged -= RefreshPlot;
        }

        // ------------------------------------------------------------------ Queries

        public bool IsField(string instanceId, out BuildingDefinition definition)
        {
            definition = null;
            return _town != null && _town.TryGet(instanceId, out var b) &&
                   _content.TryGetBuilding(b.DefinitionId, out definition) && definition.IsFarmPlot;
        }

        /// <summary>A fruit tree building (its fruit grows by itself and regrows after each harvest).</summary>
        public bool IsTree(string instanceId, out BuildingDefinition definition)
        {
            definition = null;
            return _town != null && _town.TryGet(instanceId, out var b) &&
                   _content.TryGetBuilding(b.DefinitionId, out definition) && definition.IsFruitTree;
        }

        /// <summary>A field or a fruit tree: something with a crop on it.</summary>
        public bool IsGrowingSpot(string instanceId, out BuildingDefinition definition) =>
            IsField(instanceId, out definition) || IsTree(instanceId, out definition);

        public PlotStage GetStage(string plotId) => _plots.GetStage(plotId, NowUnixMs);

        public bool IsUnlocked(CropDefinition crop) => _progress.IsUnlocked(crop.UnlockLevel);

        // ------------------------------------------------------------------ Commands

        public PlantResult Plant(string plotId, CropDefinition crop)
        {
            if (!IsField(plotId, out _)) return PlantResult.NotAField;
            if (_plots.GetStage(plotId, NowUnixMs) != PlotStage.Empty) return PlantResult.NotEmpty;
            if (!IsUnlocked(crop)) return PlantResult.Locked;
            if (!_wallet.TrySpendCoins(crop.SeedCoinCost)) { Failed?.Invoke(); return PlantResult.NotEnoughCoins; }

            _plots.TryPlant(plotId, crop.Id, crop.GrowDurationMs, NowUnixMs);
            Planted?.Invoke(plotId);
            Modified?.Invoke();
            return PlantResult.Planted;
        }

        /// <summary>Harvests a ready field into the barn and grants XP. False if not ready or the barn is full.</summary>
        public bool TryHarvest(string plotId)
        {
            if (!_plots.TryGetCrop(plotId, out var state) || !state.IsReady(NowUnixMs)) return false;
            if (!_content.TryGetCrop(state.CropId, out var crop))
            {
                // Crop removed from the game: clear the field rather than leaving it stuck.
                _plots.Clear(plotId);
                return false;
            }

            if (!_inventory.CanAdd(crop.HarvestYield))
            {
                Notification?.Invoke($"Barn is full ({_inventory.Total}/{_inventory.Capacity}). Sell items or expand the barn.");
                Failed?.Invoke();
                return false;
            }

            if (IsTree(plotId, out _)) _plots.TryHarvestAndRegrow(plotId, NowUnixMs);
            else _plots.TryHarvest(plotId, NowUnixMs);
            _inventory.TryAdd(crop.Id, crop.HarvestYield);
            if (crop.HarvestXp > 0) _progress.AddXp(crop.HarvestXp);
            Notification?.Invoke($"+{crop.HarvestYield} {crop.DisplayName}   +{crop.HarvestXp} XP");
            Harvested?.Invoke(plotId);
            Rewarded?.Invoke(new WorldReward(plotId, new[] { new ItemGain(crop, crop.HarvestYield) }, crop.HarvestXp));
            Modified?.Invoke();
            return true;
        }

        public bool FinishNow(string plotId)
        {
            if (!_plots.FinishNow(plotId, NowUnixMs)) return false;
            Modified?.Invoke();
            return true;
        }

        // ------------------------------------------------------------------ Internals

        /// <summary>Starts a fruit tree growing if nothing grows on it yet (new tree, or an old save). True if it changed.</summary>
        public bool EnsureTreeGrowing(string instanceId)
        {
            if (!IsTree(instanceId, out var def) || _plots.TryGetCrop(instanceId, out _)) return false;
            return _plots.TryPlant(instanceId, def.Fruit.Id, def.Fruit.GrowDurationMs, NowUnixMs);
        }

        /// <summary>Tap on a ready field or tree harvests it directly (no selection), the fastest farming flow.</summary>
        private bool OnBuildingTapped(PlacedBuilding building)
        {
            if (!IsGrowingSpot(building.InstanceId, out _)) return false;
            if (_plots.GetStage(building.InstanceId, NowUnixMs) != PlotStage.Ready) return false;
            TryHarvest(building.InstanceId);
            return true;
        }

        private void Update()
        {
            if (_plots == null || Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + visualRefreshInterval;
            foreach (var plotId in _plots.Crops.Keys) RefreshPlot(plotId);
        }

        private void OnBuildingAdded(PlacedBuilding building)
        {
            if (EnsureTreeGrowing(building.InstanceId)) Modified?.Invoke();
            if (IsGrowingSpot(building.InstanceId, out _)) RefreshPlot(building.InstanceId);
        }

        private void OnBuildingRemoved(PlacedBuilding building)
        {
            _cropRenderers.Remove(building.InstanceId);
            _plots.Clear(building.InstanceId);
        }

        private void RefreshPlot(string plotId)
        {
            var renderer = GetCropRenderer(plotId);
            if (renderer == null) return;

            if (_plots.TryGetCrop(plotId, out var state) && _content.TryGetCrop(state.CropId, out var crop))
            {
                renderer.sprite = crop.SpriteForProgress(state.Progress(NowUnixMs));
                renderer.enabled = renderer.sprite != null;
            }
            else
            {
                renderer.enabled = false;
            }

            // A tree's growth sprites are the whole tree with its fruit: hide the plain tree underneath.
            if (IsTree(plotId, out _) && buildings.TryGetView(plotId, out var view) && view.SpriteRenderer != null)
                view.SpriteRenderer.enabled = !renderer.enabled;
        }

        /// <summary>
        /// Crop sprite = child of the field view, on the same sorting layer and order. It sits a hair (0.001 units)
        /// lower than the field, so the Y-axis depth sort always draws it just in front of its own field while it
        /// still sorts correctly against every other building.
        /// </summary>
        private SpriteRenderer GetCropRenderer(string plotId)
        {
            if (_cropRenderers.TryGetValue(plotId, out var existing) && existing != null) return existing;
            if (!buildings.TryGetView(plotId, out var view)) return null;

            var go = new GameObject("Crop");
            go.transform.SetParent(view.transform, false);
            go.transform.localPosition = new Vector3(0f, -CropSortOffset, 0f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingLayerName = SortingLayers.Objects;
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            // Field crops sway in the wind; whole trees stay still (like the decoration trees).
            if (cropMaterial != null && !IsTree(plotId, out _)) renderer.sharedMaterial = cropMaterial;
            renderer.enabled = false;
            _cropRenderers[plotId] = renderer;
            return renderer;
        }
    }
}
