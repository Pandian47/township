using System;
using System.Collections.Generic;

namespace TownBuilder.Domain
{
    // Save DTOs. Rules:
    //  * Plain serializable fields only (JsonUtility-compatible): no Unity object references, no dictionaries.
    //  * Content is referenced by stable string ids (BuildingDefinition.Id etc.), never by asset path or name.
    //  * Adding a field is backwards compatible. Renaming/removing one requires a schemaVersion bump + SaveMigrator step.

    [Serializable]
    public sealed class SaveData
    {
        public int schemaVersion;
        public long createdAtUnixMs;
        public long savedAtUnixMs;
        public PlayerSaveData player = new PlayerSaveData();
        public List<BuildingSaveData> buildings = new List<BuildingSaveData>();
        public List<ItemStackSaveData> inventory = new List<ItemStackSaveData>();
        public List<CropPlotSaveData> cropPlots = new List<CropPlotSaveData>();
    }

    [Serializable]
    public sealed class PlayerSaveData
    {
        public int level = 1;
        public long xp;
        public long coins;
        public long gems;
    }

    [Serializable]
    public sealed class BuildingSaveData
    {
        public string instanceId;
        public string definitionId;
        public int x;
        public int y;
    }

    [Serializable]
    public sealed class ItemStackSaveData
    {
        public string itemId;
        public int amount;
    }

    /// <summary>Reserved for the farming milestone. Growth is derived from plantedAtUnixMs + crop duration.</summary>
    [Serializable]
    public sealed class CropPlotSaveData
    {
        public string plotInstanceId;
        public string cropId;
        public long plantedAtUnixMs;
    }

    public static class NewGameFactory
    {
        public static SaveData Create(long nowUnixMs, long startingCoins, long startingGems)
        {
            return new SaveData
            {
                schemaVersion = SaveMigrator.CurrentVersion,
                createdAtUnixMs = nowUnixMs,
                savedAtUnixMs = nowUnixMs,
                player = new PlayerSaveData { level = 1, xp = 0, coins = startingCoins, gems = startingGems },
            };
        }
    }
}
