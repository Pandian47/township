using System.Collections.Generic;
using TownBuilder.Domain;
using UnityEngine;

namespace TownBuilder
{
    /// <summary>
    /// A town task ("Harvest 6 Wheat", "Build a Feed Mill"). One-off tasks guide new players in list order
    /// (GameContent → Tasks); repeatable tasks keep coming back once unlocked. Rules live in <see cref="TaskBoard"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "TownBuilder/Task Definition", fileName = "Task_")]
    public sealed class TaskDefinition : ScriptableObject
    {
        [Tooltip("Permanent id (saved). lower_snake_case, never rename.")]
        [SerializeField] private string id;
        [SerializeField] private TaskKind kind;
        [Tooltip("Harvest / Collect: which crop or product (empty = any).")]
        [SerializeField] private ItemDefinition targetItem;
        [Tooltip("Build: which building.")]
        [SerializeField] private BuildingDefinition targetBuilding;
        [SerializeField, Min(1)] private int amount = 1;
        [SerializeField, Min(1)] private int unlockLevel = 1;
        [Tooltip("Repeatable tasks can come back; one-off tasks appear once, in list order.")]
        [SerializeField] private bool repeatable;

        [Header("Reward")]
        [SerializeField, Min(0)] private int rewardCoins;
        [SerializeField, Min(0)] private int rewardXp;
        [SerializeField, Min(0)] private int rewardGems;
        [Tooltip("Optional building material reward.")]
        [SerializeField] private MaterialDefinition rewardMaterial;
        [SerializeField, Min(0)] private int rewardMaterialAmount;

        public string Id => id;
        public TaskKind Kind => kind;
        public ItemDefinition TargetItem => targetItem;
        public BuildingDefinition TargetBuilding => targetBuilding;
        public int Amount => Mathf.Max(1, amount);
        public int UnlockLevel => Mathf.Max(1, unlockLevel);
        public bool Repeatable => repeatable;
        public int RewardCoins => rewardCoins;
        public int RewardXp => rewardXp;
        public int RewardGems => rewardGems;
        public MaterialDefinition RewardMaterial => rewardMaterial;
        public int RewardMaterialAmount => rewardMaterial != null ? rewardMaterialAmount : 0;

        public string TargetId => kind == TaskKind.Build
            ? (targetBuilding != null ? targetBuilding.Id : null)
            : (targetItem != null ? targetItem.Id : null);

        public TaskSpec ToSpec() =>
            new TaskSpec(id, kind, kind == TaskKind.Deliver || kind == TaskKind.LoadCrates ? null : TargetId, Amount, UnlockLevel, repeatable);

        /// <summary>Player-facing goal, e.g. "Harvest 6 Wheat".</summary>
        public string Describe()
        {
            switch (kind)
            {
                case TaskKind.Harvest: return $"Harvest {Amount} {(targetItem != null ? targetItem.DisplayName : "crops")}";
                case TaskKind.Collect: return $"Make {Amount} {(targetItem != null ? targetItem.DisplayName : "products")}";
                case TaskKind.Deliver: return Amount == 1 ? "Deliver 1 order" : $"Deliver {Amount} orders";
                case TaskKind.LoadCrates: return Amount == 1 ? "Load 1 train crate" : $"Load {Amount} train crates";
                default:
                    string name = targetBuilding != null ? targetBuilding.DisplayName : "building";
                    return Amount == 1 ? $"Build a {name}" : $"Build {Amount} × {name}";
            }
        }

        /// <summary>"+20 coins  +3 XP  +1 gem".</summary>
        public string RewardText()
        {
            var parts = new List<string>();
            if (rewardCoins > 0) parts.Add($"+{rewardCoins:N0} coins");
            if (rewardXp > 0) parts.Add($"+{rewardXp} XP");
            if (rewardGems > 0) parts.Add(rewardGems == 1 ? "+1 gem" : $"+{rewardGems} gems");
            if (RewardMaterialAmount > 0) parts.Add($"+{RewardMaterialAmount} {rewardMaterial.DisplayName}");
            return string.Join("  ", parts);
        }

        public void EditorConfigure(string newId, TaskKind newKind, ItemDefinition item, BuildingDefinition building, int newAmount,
            int newUnlockLevel, bool isRepeatable, int coins, int xp, int gems)
        {
            id = newId;
            kind = newKind;
            targetItem = item;
            targetBuilding = building;
            amount = newAmount;
            unlockLevel = newUnlockLevel;
            repeatable = isRepeatable;
            rewardCoins = coins;
            rewardXp = xp;
            rewardGems = gems;
        }

        public void EditorSetMaterialReward(MaterialDefinition material, int amount)
        {
            rewardMaterial = material;
            rewardMaterialAmount = amount;
        }
    }
}
