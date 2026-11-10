using System;
using System.Collections.Generic;
using System.Linq;
using TownBuilder.Domain;
using UnityEngine;

namespace TownBuilder
{
    /// <summary>One tier of an achievement: reach the goal, claim the gems.</summary>
    [Serializable]
    public sealed class AchievementTier
    {
        [Min(1)] public int goal = 1;
        [Min(0)] public int gems;
    }

    /// <summary>
    /// A long-term goal in tiers ("Green Fingers: harvest 50 / 300 / 1,500 crops"), registered in GameContent →
    /// Achievements. Rules live in <see cref="AchievementBook"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "TownBuilder/Achievement Definition", fileName = "Achievement_")]
    public sealed class AchievementDefinition : ScriptableObject
    {
        [Tooltip("Permanent id (saved). lower_snake_case, never rename.")]
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private AchievementKind kind;
        [Tooltip("Ascending goals, one per tier.")]
        [SerializeField] private List<AchievementTier> tiers = new List<AchievementTier>();

        public string Id => id;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? id : displayName;
        public AchievementKind Kind => kind;
        public IReadOnlyList<AchievementTier> Tiers => tiers;

        public AchievementSpec ToSpec() => new AchievementSpec(id, kind, tiers.Select(t => t.goal));

        public int GemsForTier(int tier) => tier >= 0 && tier < tiers.Count ? tiers[tier].gems : 0;

        /// <summary>Player-facing goal, e.g. "Harvest 300 crops".</summary>
        public string Describe(long goal)
        {
            string n = goal.ToString("N0");
            switch (kind)
            {
                case AchievementKind.HarvestCrops: return $"Harvest {n} crops";
                case AchievementKind.MakeProducts: return $"Make {n} products";
                case AchievementKind.DeliverOrders: return goal == 1 ? "Deliver 1 order" : $"Deliver {n} orders";
                case AchievementKind.LoadCrates: return goal == 1 ? "Load 1 train crate" : $"Load {n} train crates";
                case AchievementKind.OwnBuildings: return $"Own {n} town buildings";
                case AchievementKind.BuyLand: return goal == 1 ? "Buy 1 plot of land" : $"Buy {n} plots of land";
                default: return $"Reach level {n}";
            }
        }

        public void EditorConfigure(string newId, string name, AchievementKind newKind, List<AchievementTier> newTiers)
        {
            id = newId;
            displayName = name;
            kind = newKind;
            tiers = newTiers;
        }
    }
}
