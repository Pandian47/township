using System.Collections.Generic;
using TownBuilder.Domain;

namespace TownBuilder
{
    /// <summary>Converts between the save snapshot (DTOs) and the live domain objects of the Town scene.</summary>
    public static class TownSaveMapper
    {
        public static Wallet CreateWallet(SaveData save) => new Wallet(save.player.coins, save.player.gems);

        public static PlayerProgress CreateProgress(SaveData save) => new PlayerProgress(save.player.level, save.player.xp);

        /// <summary>Adds saved buildings to the town. Entries whose definition no longer exists or no longer fit are skipped.</summary>
        public static void LoadBuildings(SaveData save, Town town, GameContent content, List<string> warnings)
        {
            foreach (var entry in save.buildings)
            {
                if (!content.TryGetBuilding(entry.definitionId, out var definition))
                {
                    warnings.Add($"Unknown building '{entry.definitionId}' skipped.");
                    continue;
                }

                var result = town.TryAdd(entry.instanceId, entry.definitionId, new GridCoord(entry.x, entry.y),
                    definition.FootprintSize, out _);
                if (result != PlacementResult.Valid)
                    warnings.Add($"Building '{entry.definitionId}' at ({entry.x},{entry.y}) skipped: {result}.");
            }
        }

        /// <summary>
        /// Restores inventory and growing crops. Call after <see cref="LoadBuildings"/>. Crops on fields that no
        /// longer exist, or of crops removed from the game, are dropped with a warning. Growth duration comes from the
        /// current CropDefinition, so rebalancing applies to crops already in the ground.
        /// </summary>
        public static void LoadFarming(SaveData save, Town town, GameContent content, FarmPlots plots, Inventory inventory,
            List<string> warnings)
        {
            foreach (var item in save.inventory) inventory.Restore(item.itemId, item.amount);

            foreach (var plot in save.cropPlots)
            {
                if (!town.TryGet(plot.plotInstanceId, out var field) ||
                    !content.TryGetBuilding(field.DefinitionId, out var fieldDef) || !fieldDef.IsFarmPlot)
                {
                    warnings.Add($"Crop on missing field '{plot.plotInstanceId}' dropped.");
                    continue;
                }

                if (!content.TryGetCrop(plot.cropId, out var crop))
                {
                    warnings.Add($"Unknown crop '{plot.cropId}' dropped.");
                    continue;
                }

                plots.Restore(plot.plotInstanceId, crop.Id, plot.plantedAtUnixMs, crop.GrowDurationMs);
            }
        }

        public static void WriteTo(SaveData save, Town town, Wallet wallet, PlayerProgress progress, FarmPlots plots = null,
            Inventory inventory = null)
        {
            if (plots != null)
            {
                save.cropPlots.Clear();
                foreach (var pair in plots.Crops)
                {
                    save.cropPlots.Add(new CropPlotSaveData
                    {
                        plotInstanceId = pair.Key,
                        cropId = pair.Value.CropId,
                        plantedAtUnixMs = pair.Value.PlantedAtUnixMs,
                    });
                }
            }

            if (inventory != null)
            {
                save.inventory.Clear();
                foreach (var pair in inventory.Items)
                    save.inventory.Add(new ItemStackSaveData { itemId = pair.Key, amount = pair.Value });
            }

            save.player.coins = wallet.Coins;
            save.player.gems = wallet.Gems;
            save.player.level = progress.Level;
            save.player.xp = progress.Xp;

            save.buildings.Clear();
            foreach (var b in town.Buildings)
            {
                save.buildings.Add(new BuildingSaveData
                {
                    instanceId = b.InstanceId,
                    definitionId = b.DefinitionId,
                    x = b.Origin.X,
                    y = b.Origin.Y,
                });
            }
        }
    }
}
