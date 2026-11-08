using System.Collections.Generic;
using UnityEngine;

namespace TownBuilder
{
    /// <summary>
    /// Catalogue of every static definition in the game plus new-game balance values.
    /// One asset (ScriptableObjects/GameContent.asset) referenced by the scenes. Later milestones add
    /// products and recipes here the same way.
    /// </summary>
    [CreateAssetMenu(menuName = "TownBuilder/Game Content", fileName = "GameContent")]
    public sealed class GameContent : ScriptableObject
    {
        [SerializeField] private List<BuildingDefinition> buildings = new List<BuildingDefinition>();
        [SerializeField] private List<CropDefinition> crops = new List<CropDefinition>();
        [SerializeField] private List<ProductDefinition> products = new List<ProductDefinition>();
        [SerializeField] private List<RecipeDefinition> recipes = new List<RecipeDefinition>();

        [Header("New game")]
        [SerializeField] private long startingCoins = 1500;
        [SerializeField] private long startingGems = 20;
        [Tooltip("Buildings placed automatically when a new town is created.")]
        [SerializeField] private List<StarterBuilding> starterBuildings = new List<StarterBuilding>();

        [Header("Storage")]
        [Tooltip("Barn capacity before any storage buildings (Barn, Silo...) add their bonus.")]
        [SerializeField, Min(0)] private int baseStorageCapacity = 50;
        [Tooltip("Capacity added by each paid barn expansion.")]
        [SerializeField, Min(1)] private int storageExpansionCapacity = 25;
        [Tooltip("Coins for the first expansion; expansion N costs N x this.")]
        [SerializeField, Min(0)] private int storageExpansionBaseCost = 200;
        [SerializeField, Min(0)] private int storageExpansionMaxLevel = 10;

        [Header("Levels")]
        [Tooltip("XP needed for each level-up: entry 0 = level 1 to 2, entry 1 = level 2 to 3, ...")]
        [SerializeField] private List<int> xpToNextLevel = new List<int> { 15, 30, 50, 80, 120, 170, 230, 300, 380, 470 };
        [Tooltip("After the table, each level needs this much more XP than the previous one.")]
        [SerializeField, Min(0)] private int xpIncreaseAfterTable = 100;
        [SerializeField, Min(0)] private int levelUpCoinsPerLevel = 75;
        [SerializeField, Min(0)] private int levelUpGems = 1;

        [Header("Orders")]
        [Header("Factory queue upgrades")]
        [Tooltip("The n-th extra queue slot of a factory costs n × this many coins.")]
        [SerializeField, Min(0)] private int queueSlotUpgradeBaseCost = 250;
        [SerializeField, Min(0)] private int maxExtraQueueSlots = 3;

        [SerializeField, Min(1)] private int orderSlots = 3;
        [Tooltip("After skipping an order, the slot waits this long before a new order appears.")]
        [SerializeField, Min(0)] private int orderSkipCooldownSeconds = 60;

        [Header("Town tasks")]
        [Tooltip("One-off tasks guide new players in this order; repeatable ones come back at random once unlocked.")]
        [SerializeField] private List<TaskDefinition> tasks = new List<TaskDefinition>();
        [SerializeField, Min(1)] private int taskSlots = 3;

        [Header("Audio")]
        [SerializeField] private SoundLibrary soundLibrary;

        [Header("Monetisation")]
        [SerializeField] private AdsConfig adsConfig;

        private Dictionary<string, BuildingDefinition> _buildingsById;
        private Dictionary<string, CropDefinition> _cropsById;
        private Dictionary<string, ProductDefinition> _productsById;
        private Dictionary<string, RecipeDefinition> _recipesById;

        public IReadOnlyList<TaskDefinition> Tasks => tasks;
        public int TaskSlots => taskSlots;

        public bool TryGetTask(string id, out TaskDefinition task)
        {
            task = null;
            if (string.IsNullOrEmpty(id) || tasks == null) return false;
            foreach (var t in tasks)
            {
                if (t == null || t.Id != id) continue;
                task = t;
                return true;
            }

            return false;
        }

        public void EditorSetTasks(List<TaskDefinition> taskDefinitions) => tasks = taskDefinitions;

        public IReadOnlyList<ProductDefinition> Products => products;
        public IReadOnlyList<RecipeDefinition> Recipes => recipes;

        public bool TryGetRecipe(string id, out RecipeDefinition recipe)
        {
            recipe = null;
            if (string.IsNullOrEmpty(id)) return false;
            EnsureIndex();
            return _recipesById.TryGetValue(id, out recipe);
        }

        public AdsConfig AdsConfig => adsConfig;
        public SoundLibrary SoundLibrary => soundLibrary;
        public int LevelUpCoinsPerLevel => levelUpCoinsPerLevel;
        public int LevelUpGems => levelUpGems;
        public int OrderSlots => orderSlots;
        public long OrderSkipCooldownMs => orderSkipCooldownSeconds * 1000L;

        public TownBuilder.Domain.LevelCurve CreateLevelCurve() =>
            xpToNextLevel != null && xpToNextLevel.Count > 0
                ? new TownBuilder.Domain.LevelCurve(xpToNextLevel, xpIncreaseAfterTable)
                : TownBuilder.Domain.LevelCurve.Default;

        /// <summary>Names of everything that unlocks exactly at <paramref name="level"/> (for the level-up dialog).</summary>
        public List<string> UnlocksAtLevel(int level)
        {
            var names = new List<string>();
            foreach (var b in buildings) if (b != null && b.UnlockLevel == level) names.Add(b.DisplayName);
            foreach (var c in crops) if (c != null && c.UnlockLevel == level) names.Add(c.DisplayName);
            foreach (var r in recipes) if (r != null && r.UnlockLevel == level) names.Add(r.DisplayName);
            return names;
        }
        public IReadOnlyList<BuildingDefinition> Buildings => buildings;
        public int QueueSlotUpgradeBaseCost => queueSlotUpgradeBaseCost;
        public int MaxExtraQueueSlots => maxExtraQueueSlots;
        public IReadOnlyList<CropDefinition> Crops => crops;
        public IReadOnlyList<StarterBuilding> StarterBuildings => starterBuildings;
        public long StartingCoins => startingCoins < 0 ? 0 : startingCoins;
        public long StartingGems => startingGems < 0 ? 0 : startingGems;
        public int BaseStorageCapacity => baseStorageCapacity;
        public int StorageExpansionCapacity => storageExpansionCapacity;
        public int StorageExpansionBaseCost => storageExpansionBaseCost;
        public int StorageExpansionMaxLevel => storageExpansionMaxLevel;

        public bool TryGetBuilding(string id, out BuildingDefinition definition)
        {
            definition = null;
            if (string.IsNullOrEmpty(id)) return false;
            EnsureIndex();
            return _buildingsById.TryGetValue(id, out definition);
        }

        public bool TryGetCrop(string id, out CropDefinition definition)
        {
            definition = null;
            if (string.IsNullOrEmpty(id)) return false;
            EnsureIndex();
            return _cropsById.TryGetValue(id, out definition);
        }

        /// <summary>Any storable item by id (crops now, products later).</summary>
        public bool TryGetItem(string id, out ItemDefinition item)
        {
            item = null;
            if (TryGetCrop(id, out var crop)) { item = crop; return true; }
            EnsureIndex();
            if (_productsById.TryGetValue(id, out var product)) { item = product; return true; }
            return false;
        }

        /// <summary>Display name for any item id.</summary>
        public string ItemName(string itemId) => TryGetItem(itemId, out var item) ? item.DisplayName : itemId;

        /// <summary>Returns human-readable content problems. Empty list = content is valid.</summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            var seen = new HashSet<string>();
            for (int i = 0; i < buildings.Count; i++)
            {
                var b = buildings[i];
                if (b == null) { problems.Add($"Building slot {i} is empty."); continue; }
                if (string.IsNullOrEmpty(b.Id)) { problems.Add($"Building '{b.name}' has no id."); continue; }
                if (!seen.Add(b.Id)) problems.Add($"Duplicate building id '{b.Id}'.");
                if (b.Sprite == null) problems.Add($"Building '{b.Id}' has no sprite.");
            }

            var seenCrops = new HashSet<string>();
            for (int i = 0; i < crops.Count; i++)
            {
                var c = crops[i];
                if (c == null) { problems.Add($"Crop slot {i} is empty."); continue; }
                if (string.IsNullOrEmpty(c.Id)) { problems.Add($"Crop '{c.name}' has no id."); continue; }
                if (!seenCrops.Add(c.Id)) problems.Add($"Duplicate crop id '{c.Id}'.");
                if (c.SpriteForProgress(1f) == null) problems.Add($"Crop '{c.Id}' has no growth sprites.");
            }

            foreach (var r in recipes)
            {
                if (r == null) continue;
                if (r.Output == null) problems.Add($"Recipe '{r.Id}' has no output product.");
                foreach (var i in r.Ingredients)
                    if (i.item == null) problems.Add($"Recipe '{r.Id}' has an empty ingredient.");
            }

            return problems;
        }

        private void OnEnable() => ResetIndex();
        private void OnValidate() => ResetIndex();

        private void ResetIndex()
        {
            _buildingsById = null;
            _cropsById = null;
            _productsById = null;
            _recipesById = null;
        }

        private void EnsureIndex()
        {
            if (_buildingsById != null && _cropsById != null && _productsById != null && _recipesById != null) return;

            _productsById = new Dictionary<string, ProductDefinition>();
            foreach (var p in products)
                if (p != null && !string.IsNullOrEmpty(p.Id) && !_productsById.ContainsKey(p.Id)) _productsById.Add(p.Id, p);

            _recipesById = new Dictionary<string, RecipeDefinition>();
            foreach (var r in recipes)
                if (r != null && !string.IsNullOrEmpty(r.Id) && !_recipesById.ContainsKey(r.Id)) _recipesById.Add(r.Id, r);

            _buildingsById = new Dictionary<string, BuildingDefinition>();
            foreach (var b in buildings)
            {
                if (b == null || string.IsNullOrEmpty(b.Id) || _buildingsById.ContainsKey(b.Id)) continue;
                _buildingsById.Add(b.Id, b);
            }

            _cropsById = new Dictionary<string, CropDefinition>();
            foreach (var c in crops)
            {
                if (c == null || string.IsNullOrEmpty(c.Id) || _cropsById.ContainsKey(c.Id)) continue;
                _cropsById.Add(c.Id, c);
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(List<BuildingDefinition> definitions, List<StarterBuilding> starters)
        {
            buildings = definitions;
            starterBuildings = starters;
            ResetIndex();
        }

        public void EditorSetProduction(List<ProductDefinition> productDefinitions, List<RecipeDefinition> recipeDefinitions)
        {
            products = productDefinitions;
            recipes = recipeDefinitions;
            ResetIndex();
        }

        public void EditorSetCrops(List<CropDefinition> cropDefinitions)
        {
            crops = cropDefinitions;
            ResetIndex();
        }

        public void EditorSetBuildings(List<BuildingDefinition> definitions)
        {
            buildings = definitions;
            ResetIndex();
        }

        public void EditorSetStarters(List<StarterBuilding> starters) => starterBuildings = starters;
#endif
    }

    [System.Serializable]
    public struct StarterBuilding
    {
        public BuildingDefinition building;
        [Tooltip("Grid cell of the footprint's lowest corner.")]
        public Vector2Int origin;
    }
}
