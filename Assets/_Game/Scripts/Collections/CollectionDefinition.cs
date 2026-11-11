using System;
using System.Collections.Generic;
using System.Linq;
using TownBuilder.Domain;
using UnityEngine;

namespace TownBuilder
{
    /// <summary>One rare item of a collection (kept apart from the barn).</summary>
    [Serializable]
    public sealed class CollectionItem
    {
        [Tooltip("Permanent id (saved). lower_snake_case, never rename.")]
        public string id;
        public string displayName;
        public Sprite icon;
    }

    /// <summary>
    /// A collection ("Meadow Treasures"): five rare items found now and then while harvesting, making products or
    /// delivering orders. Completing it uses one of each and pays coins and gems. Rules: <see cref="CollectionBook"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "TownBuilder/Collection Definition", fileName = "Collection_")]
    public sealed class CollectionDefinition : ScriptableObject
    {
        [Tooltip("Permanent id (saved). lower_snake_case, never rename.")]
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [Tooltip("HarvestCrops (per harvest), MakeProducts (per factory collect) or DeliverOrders (per order delivered).")]
        [SerializeField] private AchievementKind source = AchievementKind.HarvestCrops;
        [Tooltip("Chance (0-1) that one such action finds an item of this collection.")]
        [SerializeField, Range(0f, 1f)] private float dropChance = 0.06f;
        [SerializeField] private List<CollectionItem> items = new List<CollectionItem>();
        [SerializeField, Min(0)] private int rewardCoins;
        [SerializeField, Min(0)] private int rewardGems;

        public string Id => id;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? id : displayName;
        public AchievementKind Source => source;
        public float DropChance => dropChance;
        public IReadOnlyList<CollectionItem> Items => items;
        public int RewardCoins => rewardCoins;
        public int RewardGems => rewardGems;

        public CollectionSetSpec ToSpec() => new CollectionSetSpec(id, source, dropChance, items.Where(i => i != null).Select(i => i.id));

        public bool TryGetItem(string itemId, out CollectionItem item)
        {
            item = items.FirstOrDefault(i => i != null && i.id == itemId);
            return item != null;
        }

        /// <summary>"Found while harvesting" etc.</summary>
        public string SourceText => source switch
        {
            AchievementKind.MakeProducts => "Found while collecting factory products",
            AchievementKind.DeliverOrders => "Found while delivering orders",
            _ => "Found while harvesting crops and fruit",
        };

        public string RewardText()
        {
            var parts = new List<string>();
            if (rewardCoins > 0) parts.Add($"{rewardCoins:N0} coins");
            if (rewardGems > 0) parts.Add(rewardGems == 1 ? "1 gem" : $"{rewardGems} gems");
            return string.Join(" + ", parts);
        }

        public void EditorConfigure(string newId, string name, AchievementKind kind, float chance, List<CollectionItem> newItems, int coins, int gems)
        {
            id = newId;
            displayName = name;
            source = kind;
            dropChance = chance;
            items = newItems;
            rewardCoins = coins;
            rewardGems = gems;
        }
    }
}
