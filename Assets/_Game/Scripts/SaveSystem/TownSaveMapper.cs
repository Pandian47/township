using System.Collections.Generic;
using TownBuilder.Domain;

namespace TownBuilder
{
    /// <summary>Converts between the save snapshot (DTOs) and the live domain objects of the Town scene.</summary>
    public static class TownSaveMapper
    {
        public static Wallet CreateWallet(SaveData save) => new Wallet(save.player.coins, save.player.gems);

        public static PlayerProgress CreateProgress(SaveData save, LevelCurve curve = null) =>
            new PlayerProgress(save.player.level, save.player.xp, curve);

        /// <summary>Restores order slots. Orders mentioning items that no longer exist are dropped (slot refilled later).</summary>
        public static void LoadOrders(SaveData save, GameContent content, OrderBoard board, List<string> warnings)
        {
            foreach (var o in save.orders)
            {
                if (o.slot < 0 || o.slot >= board.SlotCount) continue;
                var lines = new List<OrderLine>();
                bool valid = true;
                for (int i = 0; i < o.itemIds.Count; i++)
                {
                    if (!content.TryGetItem(o.itemIds[i], out _) || o.amounts[i] <= 0) { valid = false; break; }
                    lines.Add(new OrderLine(o.itemIds[i], o.amounts[i]));
                }

                if (!valid)
                {
                    warnings.Add($"Order in slot {o.slot} referenced unknown items and was replaced.");
                    continue;
                }

                board.Set(o.slot, new Order(lines, o.rewardCoins, o.rewardXp, o.availableAtUnixMs));
            }
        }

        /// <summary>Restores active tasks (unknown ids are dropped; the board refills them) and the claimed-task history.</summary>
        public static void LoadTasks(SaveData save, GameContent content, TaskBoard board, List<string> warnings)
        {
            board.RestoreCompleted(save.completedTaskIds);
            foreach (var t in save.tasks)
            {
                if (!content.TryGetTask(t.taskId, out var def))
                {
                    warnings.Add($"Unknown task '{t.taskId}' dropped.");
                    continue;
                }

                board.Restore(t.slot, def.ToSpec(), t.progress);
            }
        }

        public static void WriteTasks(SaveData save, TaskBoard board)
        {
            save.tasks.Clear();
            for (int i = 0; i < board.SlotCount; i++)
            {
                var task = board.Get(i);
                if (task != null) save.tasks.Add(new TaskSaveData { slot = i, taskId = task.Spec.Id, progress = task.Progress });
            }

            save.completedTaskIds.Clear();
            save.completedTaskIds.AddRange(board.Completed);
        }

        public static void WriteOrders(SaveData save, OrderBoard board)
        {
            save.orders.Clear();
            for (int slot = 0; slot < board.SlotCount; slot++)
            {
                var order = board.Get(slot);
                if (order == null) continue;
                var data = new OrderSaveData
                {
                    slot = slot,
                    rewardCoins = order.RewardCoins,
                    rewardXp = order.RewardXp,
                    availableAtUnixMs = order.AvailableAtUnixMs,
                };
                foreach (var line in order.Lines)
                {
                    data.itemIds.Add(line.ItemId);
                    data.amounts.Add(line.Amount);
                }

                save.orders.Add(data);
            }
        }

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

        /// <summary>Restores factory queues (in saved order). Jobs of missing factories or removed recipes are dropped.</summary>
        public static void LoadProduction(SaveData save, Town town, GameContent content, Factories factories, List<string> warnings)
        {
            foreach (var job in save.productionJobs)
            {
                if (!town.TryGet(job.factoryInstanceId, out var building) ||
                    !content.TryGetBuilding(building.DefinitionId, out var def) || !def.IsFactory)
                {
                    warnings.Add($"Production in missing factory '{job.factoryInstanceId}' dropped.");
                    continue;
                }

                if (!content.TryGetRecipe(job.recipeId, out var recipe))
                {
                    warnings.Add($"Unknown recipe '{job.recipeId}' dropped.");
                    continue;
                }

                factories.GetOrCreate(job.factoryInstanceId, def.ProductionSlots).Restore(recipe.Id, job.startUnixMs, recipe.DurationMs);
            }

            // Bought queue slots (capped by the current content, in case the maximum was lowered).
            foreach (var b in save.buildings)
            {
                if (b.extraQueueSlots <= 0 || !town.TryGet(b.instanceId, out var building)) continue;
                if (!content.TryGetBuilding(building.DefinitionId, out var def) || !def.IsFactory) continue;
                factories.SetExtraSlots(b.instanceId, System.Math.Min(b.extraQueueSlots, content.MaxExtraQueueSlots));
            }
        }

        /// <summary>Writes queues and bought queue slots. Call after <see cref="WriteTo"/> (which rebuilds the building list).</summary>
        public static void WriteProduction(SaveData save, Factories factories)
        {
            foreach (var b in save.buildings) b.extraQueueSlots = factories.ExtraSlots(b.instanceId);
            save.productionJobs.Clear();
            foreach (var pair in factories.Queues)
            {
                foreach (var job in pair.Value.Jobs)
                {
                    save.productionJobs.Add(new ProductionJobSaveData
                    {
                        factoryInstanceId = pair.Key,
                        recipeId = job.RecipeId,
                        startUnixMs = job.StartUnixMs,
                    });
                }
            }
        }

        /// <summary>Restores buildings that were still under construction. Duration comes from the current definition.</summary>
        public static void LoadConstruction(SaveData save, Town town, GameContent content, ConstructionSites sites)
        {
            foreach (var entry in save.buildings)
            {
                if (entry.constructionEndsAtUnixMs <= 0 || !town.TryGet(entry.instanceId, out _)) continue;
                if (!content.TryGetBuilding(entry.definitionId, out var def) || def.BuildDurationMs <= 0) continue;
                sites.Restore(entry.instanceId, entry.constructionEndsAtUnixMs, def.BuildDurationMs);
            }
        }

        public static void WriteTo(SaveData save, Town town, Wallet wallet, PlayerProgress progress, FarmPlots plots = null,
            Inventory inventory = null, ConstructionSites sites = null)
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
                    constructionEndsAtUnixMs = sites != null && sites.TryGet(b.InstanceId, out var site) ? site.EndUnixMs : 0,
                });
            }
        }
    }
}
