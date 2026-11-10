using System;
using System.Collections.Generic;

namespace TownBuilder.Domain
{
    public enum MigrationStatus
    {
        UpToDate,
        Migrated,
        Invalid,
        FromNewerVersion,
    }

    /// <summary>
    /// Upgrades older save files step by step (v1 -> v2 -> ...) and sanitises values.
    /// To change the save format: bump <see cref="CurrentVersion"/> and register a step that upgrades from the previous version.
    /// </summary>
    public static class SaveMigrator
    {
        public const int CurrentVersion = 1;

        // Key = version the step upgrades FROM. Example for a future v2:
        // { 1, data => { /* fill new fields from old ones */ } },
        private static readonly Dictionary<int, Action<SaveData>> Steps = new Dictionary<int, Action<SaveData>>();

        public static MigrationStatus Migrate(SaveData data, List<string> warnings = null)
        {
            if (data == null || data.schemaVersion <= 0) return MigrationStatus.Invalid;
            if (data.schemaVersion > CurrentVersion) return MigrationStatus.FromNewerVersion;

            bool migrated = false;
            while (data.schemaVersion < CurrentVersion)
            {
                if (!Steps.TryGetValue(data.schemaVersion, out var step))
                {
                    warnings?.Add($"No migration step from schema v{data.schemaVersion}.");
                    return MigrationStatus.Invalid;
                }

                step(data);
                data.schemaVersion++;
                migrated = true;
            }

            Sanitize(data, warnings);
            return migrated ? MigrationStatus.Migrated : MigrationStatus.UpToDate;
        }

        /// <summary>Repairs values that would otherwise crash or exploit the game (negative currency, duplicate ids...).</summary>
        public static void Sanitize(SaveData data, List<string> warnings = null)
        {
            data.player ??= new PlayerSaveData();
            data.buildings ??= new List<BuildingSaveData>();
            data.inventory ??= new List<ItemStackSaveData>();
            data.cropPlots ??= new List<CropPlotSaveData>();
            data.productionJobs ??= new List<ProductionJobSaveData>();
            data.orders ??= new List<OrderSaveData>();
            data.tasks ??= new List<TaskSaveData>();
            data.completedTaskIds ??= new List<string>();
            data.train ??= new TrainSaveData();
            data.land ??= new LandSaveData();
            data.daily ??= new DailySaveData();
            if (data.daily.streak < 0) data.daily.streak = 0;
            data.land.ownedPlots ??= new List<string>();
            data.land.ownedPlots.RemoveAll(id => !PlotCoord.TryParse(id, out _));
            data.train.crates ??= new List<TrainCrateSaveData>();
            data.train.crates.RemoveAll(c => c == null || string.IsNullOrEmpty(c.itemId) || c.amount <= 0);
            if (data.train.state != 0 && data.train.state != 1) data.train.state = 0;
            data.tasks.RemoveAll(t => t == null || string.IsNullOrEmpty(t.taskId));
            foreach (var t in data.tasks)
                if (t.progress < 0) t.progress = 0;

            if (data.player.level < 1) { warnings?.Add("Player level < 1, reset to 1."); data.player.level = 1; }
            if (data.player.xp < 0) { warnings?.Add("Negative XP reset to 0."); data.player.xp = 0; }
            if (data.player.coins < 0) { warnings?.Add("Negative coins reset to 0."); data.player.coins = 0; }
            if (data.player.gems < 0) { warnings?.Add("Negative gems reset to 0."); data.player.gems = 0; }
            if (data.player.storageLevel < 0) { warnings?.Add("Negative storage level reset to 0."); data.player.storageLevel = 0; }

            var seenIds = new HashSet<string>();
            data.buildings.RemoveAll(b =>
            {
                if (b == null || string.IsNullOrEmpty(b.definitionId))
                {
                    warnings?.Add("Dropped building without definition id.");
                    return true;
                }

                if (string.IsNullOrEmpty(b.instanceId)) b.instanceId = Guid.NewGuid().ToString("N");
                if (b.extraQueueSlots < 0) { warnings?.Add("Negative queue upgrades reset to 0."); b.extraQueueSlots = 0; }
                if (!seenIds.Add(b.instanceId))
                {
                    warnings?.Add($"Dropped duplicate building instance '{b.instanceId}'.");
                    return true;
                }

                return false;
            });

            data.inventory.RemoveAll(i => i == null || string.IsNullOrEmpty(i.itemId) || i.amount <= 0);
            data.cropPlots.RemoveAll(c => c == null || string.IsNullOrEmpty(c.plotInstanceId));
            data.productionJobs.RemoveAll(j => j == null || string.IsNullOrEmpty(j.factoryInstanceId) || string.IsNullOrEmpty(j.recipeId));
            data.orders.RemoveAll(o => o == null || o.itemIds == null || o.amounts == null ||
                                       o.itemIds.Count == 0 || o.itemIds.Count != o.amounts.Count);
        }
    }
}
