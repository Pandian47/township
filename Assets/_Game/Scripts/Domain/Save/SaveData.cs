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
        public List<ProductionJobSaveData> productionJobs = new List<ProductionJobSaveData>();
        public List<OrderSaveData> orders = new List<OrderSaveData>();
        public List<TaskSaveData> tasks = new List<TaskSaveData>();
        /// <summary>Ids of tasks claimed at least once (one-off guide tasks don't come back).</summary>
        public List<string> completedTaskIds = new List<string>();
        public TrainSaveData train = new TrainSaveData();
        public TruckSaveData truck = new TruckSaveData();
        /// <summary>When each home's coins were last collected (homes keep earning while the app is closed).</summary>
        public List<HomeCoinsSaveData> homeCoins = new List<HomeCoinsSaveData>();
        /// <summary>The season the player was last told about (Season as int, -1 = never).</summary>
        public int lastSeason = -1;
        /// <summary>Cells the player painted with the Landscape tool (on top of the starting map).</summary>
        public List<LandscapeCellSaveData> landscape = new List<LandscapeCellSaveData>();
        public LandSaveData land = new LandSaveData();
        public DailySaveData daily = new DailySaveData();
        public AchievementsSaveData achievements = new AchievementsSaveData();
        public EventSaveData events = new EventSaveData();
        public CollectionsSaveData collections = new CollectionsSaveData();
    }

    /// <summary>One order-board slot. itemIds[i] / amounts[i] form one line.</summary>
    [Serializable]
    public sealed class OrderSaveData
    {
        public int slot;
        public List<string> itemIds = new List<string>();
        public List<int> amounts = new List<int>();
        public long rewardCoins;
        public long rewardXp;
        public long availableAtUnixMs;
    }

    [Serializable]
    public sealed class EventSaveData
    {
        /// <summary>Local day the current event started (its Saturday), long.MinValue = none yet.</summary>
        public long key = long.MinValue;
        public string eventId;
        public int points;
        public int claimed;
        /// <summary>Event-only decorations unlocked by prizes (building ids). Kept forever.</summary>
        public List<string> unlockedDecorations = new List<string>();
    }

    [Serializable]
    public sealed class AchievementsSaveData
    {
        /// <summary>Achievement counters by kind (AchievementKind name).</summary>
        public List<AchievementCounterSaveData> counters = new List<AchievementCounterSaveData>();
        /// <summary>Claimed tiers by achievement id.</summary>
        public List<AchievementClaimSaveData> claimed = new List<AchievementClaimSaveData>();
    }

    [Serializable]
    public sealed class AchievementCounterSaveData
    {
        public string kind;
        public long value;
    }

    [Serializable]
    public sealed class AchievementClaimSaveData
    {
        public string id;
        public int tiers;
    }

    [Serializable]
    public sealed class DailySaveData
    {
        /// <summary>Local day index of the last claim (long.MinValue = never).</summary>
        public long lastClaimDay = long.MinValue;
        public int streak;
    }

    [Serializable]
    public sealed class LandSaveData
    {
        /// <summary>Owned plot ids ("x_y"). Empty = not initialised yet (start area + plots under buildings).</summary>
        public List<string> ownedPlots = new List<string>();
    }

    [Serializable]
    public sealed class CollectionsSaveData
    {
        /// <summary>Collection items owned: itemIds[i] / counts[i].</summary>
        public List<string> itemIds = new List<string>();
        public List<int> counts = new List<int>();
        /// <summary>Times each collection was completed: setIds[i] / completions[i].</summary>
        public List<string> setIds = new List<string>();
        public List<int> completions = new List<int>();
    }

    [Serializable]
    public sealed class LandscapeCellSaveData
    {
        public int x;
        public int y;
        /// <summary>Terrain name (Grass, Sand, Soil, Pavement, Water, Road).</summary>
        public string terrain;
    }

    [Serializable]
    public sealed class HomeCoinsSaveData
    {
        public string homeId;
        public long lastCollectedUnixMs;
    }

    [Serializable]
    public sealed class TruckSaveData
    {
        /// <summary>0 = at the depot, 1 = away (TruckState).</summary>
        public int state = 1;
        public long returnsAtUnixMs;
        public List<TruckLoadSaveData> loads = new List<TruckLoadSaveData>();
        /// <summary>Earnings on the way back (paid when the truck returns).</summary>
        public long cargoCoins;
        public long cargoXp;
        public List<string> cargoMaterialIds = new List<string>();
        public List<int> cargoMaterialAmounts = new List<int>();
    }

    /// <summary>One truck load. itemIds[i] / amounts[i] form one line.</summary>
    [Serializable]
    public sealed class TruckLoadSaveData
    {
        public List<string> itemIds = new List<string>();
        public List<int> amounts = new List<int>();
        public long rewardCoins;
        public long rewardXp;
        public string materialId;
        public int materialAmount;
        public bool loaded;
    }

    [Serializable]
    public sealed class TrainSaveData
    {
        /// <summary>0 = away, 1 = at the station (TrainState).</summary>
        public int state;
        public long arrivesAtUnixMs;
        public List<TrainCrateSaveData> crates = new List<TrainCrateSaveData>();
    }

    [Serializable]
    public sealed class TrainCrateSaveData
    {
        public string itemId;
        public int amount;
        public long rewardCoins;
        public long rewardXp;
        public bool filled;
        public string materialId;
        public int materialAmount;
    }

    [Serializable]
    public sealed class TaskSaveData
    {
        public int slot;
        public string taskId;
        public int progress;
    }

    /// <summary>One queued/finished product in a factory. Jobs of one factory are saved in queue order.</summary>
    [Serializable]
    public sealed class ProductionJobSaveData
    {
        public string factoryInstanceId;
        public string recipeId;
        public long startUnixMs;
    }

    [Serializable]
    public sealed class PlayerSaveData
    {
        public int level = 1;
        public long xp;
        public long coins;
        public long gems;
        /// <summary>Paid barn expansions bought (added in v0.2; missing in older saves = 0).</summary>
        public int storageLevel;
    }

    [Serializable]
    public sealed class BuildingSaveData
    {
        public string instanceId;
        public string definitionId;
        public int x;
        public int y;
        /// <summary>UTC ms when construction finishes; 0 = built (also for saves older than v0.4).</summary>
        public long constructionEndsAtUnixMs;

        /// <summary>Extra production queue slots bought for this factory (0 = none).</summary>
        public int extraQueueSlots;

        /// <summary>v0.40.0: turned to its other facing (false for older saves).</summary>
        public bool flipped;
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
