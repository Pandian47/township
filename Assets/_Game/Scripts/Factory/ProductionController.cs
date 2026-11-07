using System;
using System.Collections.Generic;
using TownBuilder.Domain;
using UnityEngine;

namespace TownBuilder
{
    public enum QueueResult
    {
        Queued,
        NotAFactory,
        UnknownRecipe,
        Locked,
        QueueFull,
        MissingIngredients,
    }

    /// <summary>
    /// Factories in the Town scene (Bakery...): queue recipes (ingredients are taken from the barn immediately),
    /// products finish one after another in real time, tap the factory to collect finished products into the barn.
    /// A small floating icon above a factory shows that something is ready to collect.
    /// </summary>
    public sealed class ProductionController : MonoBehaviour
    {
        [SerializeField] private TownBuildingController buildings;
        [SerializeField] private PlacementSystem placement;
        [Tooltip("Scale of the 'ready' product icon floating above a factory.")]
        [SerializeField] private float readyIconScale = 0.45f;
        [SerializeField] private float refreshInterval = 0.25f;

        private readonly Dictionary<string, SpriteRenderer> _readyIcons = new Dictionary<string, SpriteRenderer>();
        private Town _town;
        private Factories _factories;
        private Inventory _inventory;
        private PlayerProgress _progress;
        private GameContent _content;
        private IClock _clock;
        private float _nextRefresh;
        private Func<string, bool> _isBuilt;

        public event Action<string> Notification;
        public event Action Modified;

        public event Action<string> Queued;
        public event Action<string> Collected;
        public event Action Failed;

        public Factories Factories => _factories;
        public long NowUnixMs => _clock.UtcNowUnixMs;

        public void Initialize(Town town, Factories factories, Inventory inventory, PlayerProgress progress, GameContent content, IClock clock)
        {
            _town = town;
            _factories = factories;
            _inventory = inventory;
            _progress = progress;
            _content = content;
            _clock = clock;

            _town.BuildingRemoved += OnBuildingRemoved;
            placement.AddTapInterceptor(OnBuildingTapped);
        }

        private void OnDestroy()
        {
            if (_town != null) _town.BuildingRemoved -= OnBuildingRemoved;
        }

        // ------------------------------------------------------------------ Queries

        /// <summary>Factories under construction don't produce yet.</summary>
        public void SetBuiltCheck(Func<string, bool> isBuilt) => _isBuilt = isBuilt;

        public bool IsFactory(string instanceId, out BuildingDefinition definition)
        {
            definition = null;
            return _town != null && _town.TryGet(instanceId, out var b) &&
                   _content.TryGetBuilding(b.DefinitionId, out definition) && definition.IsFactory &&
                   (_isBuilt == null || _isBuilt(instanceId));
        }

        public ProductionQueue GetQueue(string factoryId) =>
            IsFactory(factoryId, out var def) ? _factories.GetOrCreate(factoryId, def.ProductionSlots) : null;

        public bool HasIngredients(RecipeDefinition recipe)
        {
            foreach (var i in recipe.Ingredients)
                if (i.item == null || _inventory.Count(i.item.Id) < i.amount) return false;
            return true;
        }

        public bool IsUnlocked(RecipeDefinition recipe) => _progress.IsUnlocked(recipe.UnlockLevel);

        public int CountReady(string factoryId) => _factories.TryGet(factoryId, out var q) ? q.CountDone(NowUnixMs) : 0;

        // ------------------------------------------------------------------ Commands

        public QueueResult Enqueue(string factoryId, RecipeDefinition recipe)
        {
            if (!IsFactory(factoryId, out var def)) return QueueResult.NotAFactory;
            if (recipe == null || !ContainsRecipe(def, recipe)) return QueueResult.UnknownRecipe;
            if (!IsUnlocked(recipe)) return QueueResult.Locked;

            var queue = _factories.GetOrCreate(factoryId, def.ProductionSlots);
            if (queue.IsFull)
            {
                Notification?.Invoke($"{def.DisplayName} queue is full. Collect finished products first.");
                Failed?.Invoke();
                return QueueResult.QueueFull;
            }

            if (!HasIngredients(recipe))
            {
                Notification?.Invoke($"Not enough ingredients: needs {recipe.IngredientsText()}.");
                Failed?.Invoke();
                return QueueResult.MissingIngredients;
            }

            foreach (var i in recipe.Ingredients) _inventory.TryRemove(i.item.Id, i.amount);
            queue.TryEnqueue(recipe.Id, recipe.DurationMs, NowUnixMs);
            Queued?.Invoke(factoryId);
            _factories.NotifyChanged(factoryId);
            Modified?.Invoke();
            return QueueResult.Queued;
        }

        /// <summary>Moves finished products into the barn (oldest first) until the barn is full. Returns how many were collected.</summary>
        public int Collect(string factoryId)
        {
            if (!_factories.TryGet(factoryId, out var queue)) return 0;
            int collected = 0;
            int xp = 0;
            string lastName = null;
            int lastAmount = 0;

            while (queue.CountDone(NowUnixMs) > 0)
            {
                var job = queue.Jobs[0];
                if (!_content.TryGetRecipe(job.RecipeId, out var recipe) || recipe.Output == null)
                {
                    queue.TryTakeFinished(NowUnixMs); // recipe removed from the game: drop it
                    continue;
                }

                if (!_inventory.CanAdd(recipe.OutputAmount))
                {
                    Notification?.Invoke($"Barn is full ({_inventory.Total}/{_inventory.Capacity}). Sell items or expand the barn.");
                    Failed?.Invoke();
                    break;
                }

                queue.TryTakeFinished(NowUnixMs);
                _inventory.TryAdd(recipe.Output.Id, recipe.OutputAmount);
                xp += recipe.Xp;
                collected++;
                lastName = recipe.Output.DisplayName;
                lastAmount += recipe.OutputAmount;
            }

            if (collected > 0)
            {
                if (xp > 0) _progress.AddXp(xp);
                Notification?.Invoke(collected == 1 ? $"+{lastAmount} {lastName}   +{xp} XP" : $"Collected {collected} products   +{xp} XP");
                Collected?.Invoke(factoryId);
                _factories.NotifyChanged(factoryId);
                Modified?.Invoke();
            }

            return collected;
        }

        public bool FinishActiveNow(string factoryId)
        {
            if (!_factories.TryGet(factoryId, out var queue) || !queue.FinishActiveNow(NowUnixMs)) return false;
            _factories.NotifyChanged(factoryId);
            Modified?.Invoke();
            return true;
        }

        // ------------------------------------------------------------------ Internals

        /// <summary>Tap on a factory with finished products collects them instead of selecting it.</summary>
        private bool OnBuildingTapped(PlacedBuilding building)
        {
            if (!IsFactory(building.InstanceId, out _) || CountReady(building.InstanceId) == 0) return false;
            Collect(building.InstanceId);
            return true;
        }

        private static bool ContainsRecipe(BuildingDefinition def, RecipeDefinition recipe)
        {
            foreach (var r in def.Recipes)
                if (r == recipe) return true;
            return false;
        }

        private void OnBuildingRemoved(PlacedBuilding building)
        {
            _factories.Remove(building.InstanceId);
            if (_readyIcons.TryGetValue(building.InstanceId, out var icon))
            {
                if (icon != null) Destroy(icon.gameObject);
                _readyIcons.Remove(building.InstanceId);
            }
        }

        private void Update()
        {
            if (_factories == null || Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + refreshInterval;

            foreach (var pair in _factories.Queues)
            {
                var queue = pair.Value;
                Sprite readyIcon = null;
                if (queue.CountDone(NowUnixMs) > 0 && _content.TryGetRecipe(queue.Jobs[0].RecipeId, out var recipe) && recipe.Output != null)
                    readyIcon = recipe.Output.Icon;
                UpdateReadyIcon(pair.Key, readyIcon);
            }
        }

        private void UpdateReadyIcon(string factoryId, Sprite sprite)
        {
            _readyIcons.TryGetValue(factoryId, out var renderer);
            if (sprite == null)
            {
                if (renderer != null) renderer.enabled = false;
                return;
            }

            if (renderer == null)
            {
                if (!buildings.TryGetView(factoryId, out var view)) return;
                var go = new GameObject("ReadyIcon");
                go.transform.SetParent(view.transform, false);
                renderer = go.AddComponent<SpriteRenderer>();
                renderer.sortingLayerName = SortingLayers.WorldUI;
                _readyIcons[factoryId] = renderer;
            }

            renderer.sprite = sprite;
            renderer.enabled = true;

            // Float just above the building's top.
            if (buildings.TryGetView(factoryId, out var v))
            {
                float top = v.SpriteRenderer.bounds.max.y - v.transform.position.y;
                var centerX = v.SpriteRenderer.bounds.center.x - v.transform.position.x;
                renderer.transform.localPosition = new Vector3(centerX, top + 0.3f, 0f);
                renderer.transform.localScale = Vector3.one * readyIconScale;
            }
        }
    }
}
