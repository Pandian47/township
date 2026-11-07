using UnityEngine;

namespace TownBuilder
{
    /// <summary>
    /// Static data for one crop. Growth stage sprites are drawn on top of the field with the SAME pivot convention
    /// as a 1x1 building (bottom corner of the diamond).
    /// </summary>
    [CreateAssetMenu(menuName = "TownBuilder/Crop Definition", fileName = "Crop_New")]
    public sealed class CropDefinition : ScriptableObject
    {
        [Tooltip("Stable id stored in saves and inventory. Never change it after release.")]
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [Tooltip("Shown in shop/inventory UI.")]
        [SerializeField] private Sprite icon;
        [Tooltip("Sprites from just planted to ready. The last one is shown when the crop is ready.")]
        [SerializeField] private Sprite[] growthStages = new Sprite[0];
        [SerializeField, Min(1)] private int growSeconds = 120;
        [Tooltip("Items added to the barn per harvest.")]
        [SerializeField, Min(1)] private int harvestYield = 2;
        [SerializeField, Min(0)] private int seedCoinCost;
        [SerializeField, Min(0)] private int harvestXp = 1;
        [SerializeField, Min(1)] private int unlockLevel = 1;

        public string Id => id;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? id : displayName;
        public Sprite Icon => icon != null ? icon : (growthStages.Length > 0 ? growthStages[growthStages.Length - 1] : null);
        public int GrowSeconds => Mathf.Max(1, growSeconds);
        public long GrowDurationMs => GrowSeconds * 1000L;
        public int HarvestYield => Mathf.Max(1, harvestYield);
        public int SeedCoinCost => seedCoinCost;
        public int HarvestXp => harvestXp;
        public int UnlockLevel => unlockLevel;

        /// <summary>Sprite for a growth progress of 0..1 (1 = ready).</summary>
        public Sprite SpriteForProgress(float progress)
        {
            if (growthStages == null || growthStages.Length == 0) return null;
            if (progress >= 1f) return growthStages[growthStages.Length - 1];
            int growing = Mathf.Max(1, growthStages.Length - 1); // last sprite is reserved for "ready"
            int index = Mathf.Clamp(Mathf.FloorToInt(progress * growing), 0, growing - 1);
            return growthStages[index];
        }

#if UNITY_EDITOR
        public void EditorConfigure(string newId, string newName, Sprite[] stages, int seconds, int yield, int seedCost, int xp, int unlock)
        {
            id = newId;
            displayName = newName;
            growthStages = stages;
            icon = stages != null && stages.Length > 0 ? stages[stages.Length - 1] : null;
            growSeconds = seconds;
            harvestYield = yield;
            seedCoinCost = seedCost;
            harvestXp = xp;
            unlockLevel = unlock;
        }
#endif
    }
}
