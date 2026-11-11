using System.Collections.Generic;
using TownBuilder.Domain;
using UnityEngine;

namespace TownBuilder
{
    public enum BuildingCategory
    {
        Community,
        Residential,
        Production,
        Decoration,
        Farming,
    }

    /// <summary>
    /// Static design data for one building type. Never stores player state.
    /// The sprite's pivot must sit on the BOTTOM corner of the footprint diamond (see AssetImportGuide.md).
    /// </summary>
    [CreateAssetMenu(menuName = "TownBuilder/Building Definition", fileName = "Building_New")]
    public sealed class BuildingDefinition : ScriptableObject
    {
        [Tooltip("Stable id stored in save files. Never change it after release.")]
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private BuildingCategory category;
        [SerializeField] private Sprite sprite;
        [Tooltip("Footprint in grid cells: x = along the grid X axis (right-hand front edge), y = along the grid Y axis (left-hand front edge).")]
        [SerializeField] private Vector2Int footprint = new Vector2Int(2, 2);
        [SerializeField, Min(1)] private int unlockLevel = 1;
        [SerializeField, Min(0)] private int coinCost;
        [SerializeField] private bool removable = true;
        [Tooltip("Maximum number the player may own. 0 = unlimited.")]
        [SerializeField, Min(0)] private int maxCount;
        [Tooltip("False for landmarks with a fixed place in every town (e.g. the Train Station by the railway).")]
        [SerializeField] private bool movable = true;
        [Tooltip("False for buildings the player can't buy (landmarks that every town already has).")]
        [SerializeField] private bool showInShop = true;
        [Tooltip("Chimney top on the sprite, 0-1 from the bottom-left (0,0 = no chimney). Smoke rises there while the factory works.")]
        [SerializeField] private Vector2 chimney;

        [Header("Gameplay roles")]
        [Tooltip("This building is a field where crops can be planted.")]
        [SerializeField] private bool isFarmPlot;
        [Tooltip("Fruit tree: this crop grows on it by itself and regrows after every harvest (no planting).")]
        [SerializeField] private CropDefinition fruit;
        [Tooltip("Extra barn capacity this building adds (e.g. Barn, Silo).")]
        [SerializeField, Min(0)] private int storageCapacityBonus;
        [Tooltip("Recipes this building can produce. Any recipe makes it a factory.")]
        [SerializeField] private List<RecipeDefinition> recipes = new List<RecipeDefinition>();
        [Tooltip("How many products can be queued (including finished ones waiting to be collected).")]
        [SerializeField, Min(1)] private int productionSlots = 3;

        [Header("Construction")]
        [Tooltip("Seconds to build after placing. 0 = ready instantly.")]
        [SerializeField, Min(0)] private int buildSeconds;
        [Tooltip("XP granted when construction completes.")]
        [SerializeField, Min(0)] private int buildXp;

        public string Id => id;
        public int MaxCount => maxCount;
        public bool IsFarmPlot => isFarmPlot;
        public CropDefinition Fruit => fruit;
        public bool IsFruitTree => fruit != null;
        public int StorageCapacityBonus => storageCapacityBonus;
        public IReadOnlyList<RecipeDefinition> Recipes => recipes;
        public bool IsFactory => recipes != null && recipes.Count > 0;
        public int ProductionSlots => Mathf.Max(1, productionSlots);
        public long BuildDurationMs => buildSeconds * 1000L;
        public int BuildXp => buildXp;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? id : displayName;
        public BuildingCategory Category => category;
        public Sprite Sprite => sprite;
        public GridCoord FootprintSize => new GridCoord(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y));
        public int UnlockLevel => unlockLevel;
        public int CoinCost => coinCost;
        public bool Removable => removable;
        public bool Movable => movable;
        public bool ShowInShop => showInShop;
        public Vector2 ChimneyPoint => chimney;
        public bool HasChimney => chimney != Vector2.zero;

        public void EditorSetChimney(Vector2 point) => chimney = point;
        public void EditorSetFruit(CropDefinition crop) => fruit = crop;
        public void EditorSetCategory(BuildingCategory newCategory) => category = newCategory;

        public void EditorConfigureLandmark(bool isMovable, bool inShop)
        {
            movable = isMovable;
            showInShop = inShop;
        }

        private void OnValidate()
        {
            footprint = new Vector2Int(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y));
            if (id != null) id = id.Trim();
        }

#if UNITY_EDITOR
        public void EditorConfigure(string newId, string newName, BuildingCategory newCategory, Sprite newSprite,
            Vector2Int newFootprint, int newUnlockLevel, int newCoinCost, bool isRemovable, int newMaxCount)
        {
            maxCount = newMaxCount;
            id = newId;
            displayName = newName;
            category = newCategory;
            sprite = newSprite;
            footprint = newFootprint;
            unlockLevel = newUnlockLevel;
            coinCost = newCoinCost;
            removable = isRemovable;
        }

        public void EditorConfigureConstruction(int seconds, int xp)
        {
            buildSeconds = seconds;
            buildXp = xp;
        }

        public void EditorConfigureFactory(List<RecipeDefinition> newRecipes, int slots)
        {
            recipes = newRecipes;
            productionSlots = slots;
        }

        public void EditorConfigureRoles(bool farmPlot, int storageBonus)
        {
            isFarmPlot = farmPlot;
            storageCapacityBonus = storageBonus;
        }
#endif
    }
}
