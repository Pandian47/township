using System.Collections.Generic;
using System.Linq;
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

        /// <summary>Restores the train. Crates for items that no longer exist are dropped.</summary>
        public static void LoadTrain(SaveData save, GameContent content, Train train, List<string> warnings)
        {
            var crates = new List<TrainCrate>();
            foreach (var c in save.train.crates)
            {
                if (!content.TryGetItem(c.itemId, out _))
                {
                    warnings.Add($"Train crate for unknown item '{c.itemId}' dropped.");
                    continue;
                }

                string material = !string.IsNullOrEmpty(c.materialId) && content.TryGetItem(c.materialId, out _) ? c.materialId : null;
                crates.Add(new TrainCrate(c.itemId, c.amount, c.rewardCoins, c.rewardXp, c.filled, material, c.materialAmount));
            }

            train.Restore((TrainState)save.train.state, save.train.arrivesAtUnixMs, crates);
        }

        /// <summary>Restores the delivery truck (loads with unknown items are dropped).</summary>
        public static void LoadTruck(SaveData save, GameContent content, DeliveryTruck truck, List<string> warnings)
        {
            var loads = new List<TruckLoad>();
            foreach (var l in save.truck.loads)
            {
                var lines = new List<TruckLine>();
                bool ok = true;
                for (int i = 0; i < l.itemIds.Count && i < l.amounts.Count; i++)
                {
                    if (!content.TryGetItem(l.itemIds[i], out _)) { ok = false; break; }
                    lines.Add(new TruckLine(l.itemIds[i], l.amounts[i]));
                }

                if (!ok || lines.Count == 0)
                {
                    warnings.Add("Truck load with an unknown item dropped.");
                    continue;
                }

                string material = !string.IsNullOrEmpty(l.materialId) && content.TryGetItem(l.materialId, out _) ? l.materialId : null;
                loads.Add(new TruckLoad(lines, l.rewardCoins, l.rewardXp, material, l.materialAmount, l.loaded));
            }

            var cargo = new TruckPayout { Coins = save.truck.cargoCoins, Xp = save.truck.cargoXp };
            for (int i = 0; i < save.truck.cargoMaterialIds.Count && i < save.truck.cargoMaterialAmounts.Count; i++)
                if (content.TryGetItem(save.truck.cargoMaterialIds[i], out _))
                    cargo.Materials[save.truck.cargoMaterialIds[i]] = save.truck.cargoMaterialAmounts[i];
            truck.Restore((TruckState)save.truck.state, save.truck.returnsAtUnixMs, loads, cargo);
        }

        public static HomeCoins LoadHomeCoins(SaveData save, Town town)
        {
            var coins = new HomeCoins();
            foreach (var h in save.homeCoins)
                if (town.TryGet(h.homeId, out _)) coins.Restore(h.homeId, h.lastCollectedUnixMs);
            return coins;
        }

        public static void WriteHomeCoins(SaveData save, HomeCoins coins)
        {
            save.homeCoins.Clear();
            foreach (var pair in coins.LastCollected)
                save.homeCoins.Add(new HomeCoinsSaveData { homeId = pair.Key, lastCollectedUnixMs = pair.Value });
        }

        public static void WriteTruck(SaveData save, DeliveryTruck truck)
        {
            var t = save.truck;
            t.state = (int)truck.State;
            t.returnsAtUnixMs = truck.ReturnsAtUnixMs;
            t.loads.Clear();
            foreach (var l in truck.Loads)
                t.loads.Add(new TruckLoadSaveData
                {
                    itemIds = l.Lines.Select(x => x.ItemId).ToList(), amounts = l.Lines.Select(x => x.Amount).ToList(),
                    rewardCoins = l.RewardCoins, rewardXp = l.RewardXp, materialId = l.MaterialId, materialAmount = l.MaterialAmount, loaded = l.Loaded,
                });
            t.cargoCoins = truck.Cargo.Coins;
            t.cargoXp = truck.Cargo.Xp;
            t.cargoMaterialIds = truck.Cargo.Materials.Keys.ToList();
            t.cargoMaterialAmounts = truck.Cargo.Materials.Values.ToList();
        }

        public static void WriteTrain(SaveData save, Train train)
        {
            save.train.state = (int)train.State;
            save.train.arrivesAtUnixMs = train.ArrivesAtUnixMs;
            save.train.crates.Clear();
            foreach (var c in train.Crates)
                save.train.crates.Add(new TrainCrateSaveData
                {
                    itemId = c.ItemId, amount = c.Amount, rewardCoins = c.RewardCoins, rewardXp = c.RewardXp, filled = c.Filled,
                    materialId = c.MaterialId, materialAmount = c.MaterialAmount,
                });
        }

        /// <summary>
        /// Restores owned land. Old saves (no land yet) and new towns get the start area plus every plot that has a
        /// building on it; plots under buildings are always kept owned.
        /// </summary>
        public static void LoadLand(SaveData save, Town town, LandPlots land, GameContent content = null)
        {
            var occupied = new List<GridCoord>();
            foreach (var b in town.Buildings)
            {
                // Fixed landmarks don't make the land around them free.
                if (content != null && content.TryGetBuilding(b.DefinitionId, out var def) && !def.Movable) continue;
                for (int dx = 0; dx < b.Size.X; dx++)
                for (int dy = 0; dy < b.Size.Y; dy++)
                    occupied.Add(new GridCoord(b.Origin.X + dx, b.Origin.Y + dy));
            }

            if (save.land.ownedPlots.Count == 0)
            {
                land.InitializeDefault(occupied);
                return;
            }

            var owned = new List<PlotCoord>();
            foreach (var id in save.land.ownedPlots)
                if (PlotCoord.TryParse(id, out var p)) owned.Add(p);
            foreach (var cell in occupied) owned.Add(land.PlotOf(cell));
            land.Restore(owned);
        }

        /// <summary>Restores achievement counters (unknown kinds dropped) and claimed tiers (unknown ids dropped).</summary>
        public static EventProgress LoadEvents(SaveData save)
        {
            var progress = new EventProgress();
            if (save.events.key != long.MinValue)
                progress.Restore(save.events.key, save.events.eventId, save.events.points, save.events.claimed);
            return progress;
        }

        public static void WriteEvents(SaveData save, WeekendEventService events)
        {
            save.events.key = events.Progress.Key;
            save.events.eventId = events.Progress.EventId;
            save.events.points = events.Progress.Points;
            save.events.claimed = events.Progress.Claimed;
            save.events.unlockedDecorations.Clear();
            save.events.unlockedDecorations.AddRange(events.UnlockedDecorations);
        }

        public static void LoadAchievements(SaveData save, AchievementBook book)
        {
            var counts = new List<KeyValuePair<AchievementKind, long>>();
            foreach (var c in save.achievements.counters)
                if (c != null && System.Enum.TryParse(c.kind, out AchievementKind kind))
                    counts.Add(new KeyValuePair<AchievementKind, long>(kind, c.value));
            var claimed = new List<KeyValuePair<string, int>>();
            foreach (var c in save.achievements.claimed)
                if (c != null && !string.IsNullOrEmpty(c.id)) claimed.Add(new KeyValuePair<string, int>(c.id, c.tiers));
            book.Restore(counts, claimed);
        }

        public static void WriteAchievements(SaveData save, AchievementBook book)
        {
            save.achievements.counters.Clear();
            foreach (var c in book.Counts)
                save.achievements.counters.Add(new AchievementCounterSaveData { kind = c.Key.ToString(), value = c.Value });
            save.achievements.claimed.Clear();
            foreach (var c in book.Claimed)
                save.achievements.claimed.Add(new AchievementClaimSaveData { id = c.Key, tiers = c.Value });
        }

        public static DailyRewardTrack LoadDaily(SaveData save, int calendarLength) =>
            new DailyRewardTrack(calendarLength, save.daily.lastClaimDay, save.daily.streak);

        public static void WriteDaily(SaveData save, DailyRewardTrack track)
        {
            save.daily.lastClaimDay = track.LastClaimDay;
            save.daily.streak = track.Streak;
        }

        public static void WriteLand(SaveData save, LandPlots land)
        {
            save.land.ownedPlots.Clear();
            foreach (var p in land.Owned) save.land.ownedPlots.Add(p.Id);
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

                var origin = new GridCoord(entry.x, entry.y);
                var result = town.TryAdd(entry.instanceId, entry.definitionId, origin, definition.FootprintSize, out _);
                if (result == PlacementResult.Valid) continue;

                // The map changed under it (e.g. new railway): move it to the nearest free spot rather than lose it.
                if (TryFindFreeOrigin(town, origin, definition.FootprintSize, null, out var free) &&
                    town.TryAdd(entry.instanceId, entry.definitionId, free, definition.FootprintSize, out _) == PlacementResult.Valid)
                    warnings.Add($"Building '{entry.definitionId}' moved from ({entry.x},{entry.y}) to ({free.X},{free.Y}): {result}.");
                else
                    warnings.Add($"Building '{entry.definitionId}' at ({entry.x},{entry.y}) skipped: {result}.");
            }
        }

        /// <summary>
        /// Fixed landmarks (non-movable starter buildings, e.g. the Train Station) always stand at their starter spot.
        /// Old saves get them added; a player building in the way is moved to the nearest free spot.
        /// </summary>
        public static void EnsureFixedBuildings(Town town, GameContent content, List<string> warnings)
        {
            foreach (var starter in content.StarterBuildings)
            {
                var def = starter.building;
                if (def == null || def.Movable) continue;
                var origin = new GridCoord(starter.origin.x, starter.origin.y);
                var size = def.FootprintSize;
                var existing = town.Buildings.FirstOrDefault(b => b.DefinitionId == def.Id);
                if (existing != null && existing.Origin == origin) continue;

                string id = existing != null ? existing.InstanceId : System.Guid.NewGuid().ToString("N");
                if (existing != null) town.Remove(existing.InstanceId);

                // Clear the spot: move whatever stands there out of the landmark's footprint.
                var target = new GridRect(origin.X, origin.Y, size.X, size.Y);
                foreach (var other in town.Buildings.ToList())
                {
                    var rect = new GridRect(other.Origin.X, other.Origin.Y, other.Size.X, other.Size.Y);
                    if (!Overlaps(rect, target)) continue;
                    if (TryFindFreeOrigin(town, other.Origin, other.Size, target, out var free, other.InstanceId) &&
                        town.TryMove(other.InstanceId, free) == PlacementResult.Valid)
                        warnings.Add($"'{other.DefinitionId}' moved to ({free.X},{free.Y}) to make room for the {def.DisplayName}.");
                }

                var result = town.TryAdd(id, def.Id, origin, size, out _);
                if (result != PlacementResult.Valid) warnings.Add($"{def.DisplayName} could not be placed at ({origin.X},{origin.Y}): {result}.");
            }
        }

        private static bool Overlaps(GridRect a, GridRect b) =>
            a.MinX < b.MaxX && b.MinX < a.MaxX && a.MinY < b.MaxY && b.MinY < a.MaxY;

        /// <summary>Spiral search for the closest origin where the footprint fits (optionally avoiding a reserved rect).</summary>
        private static bool TryFindFreeOrigin(Town town, GridCoord start, GridCoord size, GridRect? avoid, out GridCoord found,
            string ignoreId = null)
        {
            for (int r = 1; r <= 20; r++)
            for (int dx = -r; dx <= r; dx++)
            for (int dy = -r; dy <= r; dy++)
            {
                if (System.Math.Abs(dx) != r && System.Math.Abs(dy) != r) continue;
                var candidate = new GridCoord(start.X + dx, start.Y + dy);
                if (avoid.HasValue && Overlaps(new GridRect(candidate.X, candidate.Y, size.X, size.Y), avoid.Value)) continue;
                if (town.CanPlace(candidate, size, ignoreId) != PlacementResult.Valid) continue;
                found = candidate;
                return true;
            }

            found = start;
            return false;
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
                    !content.TryGetBuilding(field.DefinitionId, out var fieldDef) || !(fieldDef.IsFarmPlot || fieldDef.IsFruitTree))
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
