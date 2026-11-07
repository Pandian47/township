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

        [Header("Monetisation")]
        [SerializeField] private AdsConfig adsConfig;

        private Dictionary<string, BuildingDefinition> _buildingsById;
        private Dictionary<string, CropDefinition> _cropsById;

        public AdsConfig AdsConfig => adsConfig;
        public IReadOnlyList<BuildingDefinition> Buildings => buildings;
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

            return problems;
        }

        private void OnEnable() => ResetIndex();
        private void OnValidate() => ResetIndex();

        private void ResetIndex()
        {
            _buildingsById = null;
            _cropsById = null;
        }

        private void EnsureIndex()
        {
            if (_buildingsById != null && _cropsById != null) return;
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
