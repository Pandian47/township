using System.Collections.Generic;
using UnityEngine;

namespace TownBuilder
{
    /// <summary>
    /// Catalogue of every static definition in the game plus new-game balance values.
    /// One asset (ScriptableObjects/GameContent.asset) referenced by the Boot scene. Later milestones add
    /// crops, products and recipes here the same way.
    /// </summary>
    [CreateAssetMenu(menuName = "TownBuilder/Game Content", fileName = "GameContent")]
    public sealed class GameContent : ScriptableObject
    {
        [SerializeField] private List<BuildingDefinition> buildings = new List<BuildingDefinition>();

        [Header("New game")]
        [SerializeField] private long startingCoins = 1500;
        [SerializeField] private long startingGems = 20;
        [Tooltip("Buildings placed automatically when a new town is created.")]
        [SerializeField] private List<StarterBuilding> starterBuildings = new List<StarterBuilding>();

        private Dictionary<string, BuildingDefinition> _buildingsById;

        public IReadOnlyList<BuildingDefinition> Buildings => buildings;
        public IReadOnlyList<StarterBuilding> StarterBuildings => starterBuildings;
        public long StartingCoins => startingCoins < 0 ? 0 : startingCoins;
        public long StartingGems => startingGems < 0 ? 0 : startingGems;

        public bool TryGetBuilding(string id, out BuildingDefinition definition)
        {
            definition = null;
            if (string.IsNullOrEmpty(id)) return false;
            EnsureIndex();
            return _buildingsById.TryGetValue(id, out definition);
        }

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

            return problems;
        }

        private void OnEnable() => _buildingsById = null;
        private void OnValidate() => _buildingsById = null;

        private void EnsureIndex()
        {
            if (_buildingsById != null) return;
            _buildingsById = new Dictionary<string, BuildingDefinition>();
            foreach (var b in buildings)
            {
                if (b == null || string.IsNullOrEmpty(b.Id) || _buildingsById.ContainsKey(b.Id)) continue;
                _buildingsById.Add(b.Id, b);
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(List<BuildingDefinition> definitions, List<StarterBuilding> starters)
        {
            buildings = definitions;
            starterBuildings = starters;
            _buildingsById = null;
        }
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
