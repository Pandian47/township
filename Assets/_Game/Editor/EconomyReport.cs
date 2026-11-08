using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TownBuilder.EditorTools
{
    /// <summary>
    /// A deliberately simple economy model used for balancing (not a simulation). For each level it estimates how long
    /// the level takes with "typical" play and whether the player can afford the buildings unlocked on the way.
    ///
    /// Assumptions (keep them in sync with Documentation/GameEconomy.md):
    /// * Fields: 3 at level 1, one more per level (max 12), always growing the crop that earns the most coins.
    /// * One of each unlocked factory, always making its recipe with the best value added per minute.
    /// * Players are about half as efficient as non-stop perfect play (<see cref="Efficiency"/>).
    /// * Crops are sold to the barn; orders (which pay ~60% more) are ignored, so the estimate errs on the poor side.
    /// * The player buys one of each new factory/storage building (and the Train Station) when it unlocks.
    ///   Cottages and decorations are optional.
    /// * Land: the first plot of each ring is bought (coins) the level it unlocks. Materials come from train crates and
    ///   repeatable tasks: <c>TaskSlots</c> tasks done every <see cref="TaskMinutes"/> minutes, picked evenly from the
    ///   unlocked repeatable tasks. The report shows when the first plot's materials have been earned.
    /// * Train (once its station is unlocked): one visit per travel time + <see cref="TrainLoadingMinutes"/>, crates of the
    ///   average item the player can make; income counted is the extra over selling those items to the barn.
    ///
    /// Menu: TownBuilder > Debug > Write Economy Report (writes Documentation/EconomyReport.md).
    /// </summary>
    public static class EconomyReport
    {
        public const float Efficiency = 0.5f;
        public const float TrainLoadingMinutes = 15f;
        public const float TaskMinutes = 12f;

        /// <summary>When the materials for the first plot of a land ring have been earned.</summary>
        public sealed class LandStatus
        {
            public int Ring;
            public int UnlockLevel;
            public int Coins;
            public string Materials;
            /// <summary>Level during which the materials are complete, or -1 if not within the report levels.</summary>
            public int MaterialsReadyLevel = -1;
        }
        public const int ReportLevels = 10;

        public sealed class Rate
        {
            public string Id;
            public string Name;
            public string Kind;
            public int Unlock;
            public float Minutes;
            public float CoinsPerMinute;
            public float XpPerMinute;
        }

        public sealed class LevelRow
        {
            public int Level;
            public long XpToNext;
            public int Fields;
            public float CoinsPerMinute;
            public float XpPerMinute;
            public float Minutes;
            public float TrainCoinsPerMinute;
            public long Earned;
            /// <summary>Materials earned during this level, by material id.</summary>
            public Dictionary<string, float> MaterialsEarned = new Dictionary<string, float>();
            public long LevelUpReward;
            public long BuildingCosts;
            public string NewBuildings;
            /// <summary>Coins left after buying this level's new buildings (on arrival) — negative means the player has to wait.</summary>
            public long BalanceAfterBuying;
            public long BalanceAtEnd;
        }

        public static List<Rate> Rates(GameContent content)
        {
            var rates = new List<Rate>();
            foreach (var crop in content.Crops)
            {
                float minutes = crop.GrowSeconds / 60f;
                rates.Add(new Rate
                {
                    Id = crop.Id, Name = crop.DisplayName, Kind = "Crop (per field)", Unlock = crop.UnlockLevel, Minutes = minutes,
                    CoinsPerMinute = (crop.HarvestYield * crop.SellPrice - crop.SeedCoinCost) / minutes,
                    XpPerMinute = crop.HarvestXp / minutes,
                });
            }

            foreach (var factory in content.Buildings.Where(b => b.IsFactory))
            foreach (var recipe in factory.Recipes)
            {
                float minutes = recipe.DurationSeconds / 60f;
                int raw = recipe.Ingredients.Sum(i => i.item.SellPrice * i.amount);
                rates.Add(new Rate
                {
                    Id = recipe.Id, Name = recipe.DisplayName, Kind = factory.DisplayName,
                    Unlock = Math.Max(recipe.UnlockLevel, factory.UnlockLevel), Minutes = minutes,
                    CoinsPerMinute = (recipe.Output.SellPrice * recipe.OutputAmount - raw) / minutes,
                    XpPerMinute = recipe.Xp / minutes,
                });
            }

            return rates;
        }

        public static List<LevelRow> Levels(GameContent content) => Simulate(content, out _);

        public static List<LandStatus> Land(GameContent content)
        {
            Simulate(content, out var land);
            return land;
        }

        private static List<LevelRow> Simulate(GameContent content, out List<LandStatus> land)
        {
            land = new List<LandStatus>();
            for (int i = 0; i < content.LandTiers.Count; i++)
            {
                var tier = content.LandTiers[i];
                land.Add(new LandStatus
                {
                    Ring = i + 1, UnlockLevel = tier.unlockLevel, Coins = tier.coins,
                    Materials = string.Join(" + ", tier.materials.Where(m => m.material != null).Select(m => $"{m.amount} {m.material.DisplayName}")),
                });
            }

            var stock = content.Materials.Where(m => m != null).ToDictionary(m => m.Id, _ => 0f);
            var curve = content.CreateLevelCurve();
            var rows = new List<LevelRow>();
            long coins = content.StartingCoins;
            var keyBuildings = content.Buildings.Where(b => b.IsFactory || b.StorageCapacityBonus > 0 || b.Id == content.TrainStationId).ToList();

            for (int level = 1; level <= ReportLevels; level++)
            {
                var row = new LevelRow { Level = level, XpToNext = curve.XpToNext(level), Fields = Math.Min(12, 2 + level) };
                row.LevelUpReward = level > 1 ? (long)content.LevelUpCoinsPerLevel * level : 0;
                coins += row.LevelUpReward;

                var newOnes = keyBuildings.Where(b => b.UnlockLevel == level).ToList();
                row.BuildingCosts = newOnes.Sum(b => (long)b.CoinCost);
                var names = newOnes.Select(b => b.DisplayName).ToList();
                foreach (var l in land.Where(l => l.UnlockLevel == level))
                {
                    row.BuildingCosts += l.Coins;
                    names.Add($"Land ring {l.Ring}");
                }

                row.NewBuildings = names.Count > 0 ? string.Join(", ", names) : "-";
                coins -= row.BuildingCosts;
                row.BalanceAfterBuying = coins;

                var bestCrop = content.Crops.Where(c => c.UnlockLevel <= level)
                    .OrderByDescending(c => (c.HarvestYield * c.SellPrice - c.SeedCoinCost) / (float)c.GrowSeconds).First();
                float cropMinutes = bestCrop.GrowSeconds / 60f;
                float cpm = row.Fields * (bestCrop.HarvestYield * bestCrop.SellPrice - bestCrop.SeedCoinCost) / cropMinutes;
                float xpm = row.Fields * bestCrop.HarvestXp / cropMinutes;

                foreach (var factory in content.Buildings.Where(b => b.IsFactory && b.UnlockLevel <= level))
                {
                    Rate best = null;
                    foreach (var recipe in factory.Recipes.Where(r => r.UnlockLevel <= level))
                    {
                        float minutes = recipe.DurationSeconds / 60f;
                        int raw = recipe.Ingredients.Sum(i => i.item.SellPrice * i.amount);
                        var rate = new Rate
                        {
                            CoinsPerMinute = (recipe.Output.SellPrice * recipe.OutputAmount - raw) / minutes,
                            XpPerMinute = recipe.Xp / minutes,
                        };
                        if (best == null || rate.CoinsPerMinute > best.CoinsPerMinute) best = rate;
                    }

                    if (best == null) continue;
                    cpm += best.CoinsPerMinute;
                    xpm += best.XpPerMinute;
                }

                var station = content.Buildings.FirstOrDefault(b => b.Id == content.TrainStationId);
                if (station != null && station.UnlockLevel <= level)
                {
                    TrainRates(content, level, out float trainCpm, out float trainXpm);
                    row.TrainCoinsPerMinute = trainCpm * Efficiency;
                    cpm += trainCpm;
                    xpm += trainXpm;
                }

                row.CoinsPerMinute = cpm * Efficiency;
                row.XpPerMinute = xpm * Efficiency;
                row.Minutes = row.XpPerMinute > 0 ? row.XpToNext / row.XpPerMinute : float.PositiveInfinity;
                row.Earned = (long)(row.CoinsPerMinute * row.Minutes);
                coins += row.Earned;
                row.BalanceAtEnd = coins;

                // Materials earned this level (repeatable tasks + train), plus one-off guide tasks unlocking now.
                foreach (var pair in MaterialRates(content, level))
                {
                    float earned = pair.Value * row.Minutes;
                    row.MaterialsEarned[pair.Key] = earned;
                    stock[pair.Key] += earned;
                }

                foreach (var t in content.Tasks.Where(t => t != null && !t.Repeatable && t.UnlockLevel == level && t.RewardMaterialAmount > 0))
                {
                    stock[t.RewardMaterial.Id] += t.RewardMaterialAmount;
                    row.MaterialsEarned[t.RewardMaterial.Id] = row.MaterialsEarned.TryGetValue(t.RewardMaterial.Id, out var e) ? e + t.RewardMaterialAmount : t.RewardMaterialAmount;
                }

                // First plot of each unlocked ring: buy as soon as its materials are there (rings in order).
                for (int i = 0; i < land.Count; i++)
                {
                    var status = land[i];
                    if (status.MaterialsReadyLevel >= 0 || status.UnlockLevel > level) continue;
                    var tier = content.LandTiers[i];
                    if (!tier.materials.All(m => m.material == null || stock[m.material.Id] >= m.amount - 0.001f)) break;
                    foreach (var m in tier.materials.Where(m => m.material != null)) stock[m.material.Id] -= m.amount;
                    status.MaterialsReadyLevel = level;
                }

                rows.Add(row);
            }

            return rows;
        }

        /// <summary>Building materials earned per minute at a level, by material id (repeatable tasks + train crates).</summary>
        public static Dictionary<string, float> MaterialRates(GameContent content, int level)
        {
            var rates = content.Materials.Where(m => m != null).ToDictionary(m => m.Id, _ => 0f);
            if (rates.Count == 0) return rates;

            var repeatables = content.Tasks.Where(t => t != null && t.Repeatable && t.UnlockLevel <= level).ToList();
            foreach (var t in repeatables.Where(t => t.RewardMaterialAmount > 0 && rates.ContainsKey(t.RewardMaterial.Id)))
                rates[t.RewardMaterial.Id] += content.TaskSlots / TaskMinutes * t.RewardMaterialAmount / repeatables.Count;

            var station = content.Buildings.FirstOrDefault(b => b.Id == content.TrainStationId);
            if (station != null && station.UnlockLevel <= level)
            {
                float visitMinutes = content.TrainTravelSeconds / 60f + TrainLoadingMinutes;
                float perMinute = content.TrainCrates * content.TrainMaterialChance / visitMinutes * Efficiency;
                foreach (var id in rates.Keys.ToList()) rates[id] += perMinute / rates.Count;
            }

            return rates;
        }

        /// <summary>Train income per minute at a level: extra coins over selling the crate items, and XP.</summary>
        public static void TrainRates(GameContent content, int level, out float coinsPerMinute, out float xpPerMinute)
        {
            // Same candidates as orders: unlocked crops and products of unlocked recipes in unlocked factories.
            var values = content.Crops.Where(c => c.UnlockLevel <= level).Select(c => c.SellPrice).ToList();
            foreach (var factory in content.Buildings.Where(b => b.IsFactory && b.UnlockLevel <= level))
                values.AddRange(factory.Recipes.Where(r => r.UnlockLevel <= level).Select(r => r.Output.SellPrice));

            coinsPerMinute = 0f;
            xpPerMinute = 0f;
            if (values.Count == 0) return;

            // Expected crate value, matching TrainCrateGenerator's amounts (3-6 / 2-3 / 1-2 by item value).
            float crateValue = (float)values.Average(v => v * (v <= 10 ? 4.5 : v <= 40 ? 2.5 : 1.5));
            float visitValue = crateValue * content.TrainCrates;
            float visitMinutes = content.TrainTravelSeconds / 60f + TrainLoadingMinutes;
            float extraShare = content.TrainCoinMultiplier - 1f + content.TrainCoinMultiplier * content.TrainFullBonusFraction;
            coinsPerMinute = visitValue * extraShare / visitMinutes;
            xpPerMinute = visitValue / content.TrainXpDivisor / visitMinutes;
        }

        [MenuItem("TownBuilder/Debug/Write Economy Report", priority = 41)]
        public static void WriteMenu()
        {
            var path = Write();
            EditorUtility.RevealInFinder(path);
        }

        public static void WriteFromCommandLine()
        {
            try { Debug.Log("[Economy] Wrote " + Write()); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static string Write()
        {
            var content = AssetDatabase.LoadAssetAtPath<GameContent>(ProjectSetup.ContentPath);
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine("# Economy report (generated)");
            sb.AppendLine();
            sb.AppendLine("Generated by **TownBuilder → Debug → Write Economy Report** (`Assets/_Game/Editor/EconomyReport.cs`) from the");
            sb.AppendLine("current content. Don't edit by hand; change the assets and regenerate. The model's assumptions are listed in");
            sb.AppendLine("GameEconomy.md (\"Balance model\"). It is a rough guide for balancing, not a prediction.");
            sb.AppendLine();
            sb.AppendLine("## Earning rates (per field or per factory, non-stop)");
            sb.AppendLine();
            sb.AppendLine("Coins/min for recipes is the value added (product value minus the ingredients' sell value).");
            sb.AppendLine();
            sb.AppendLine("| Item | Made in | Unlock | Time (min) | Coins/min | XP/min |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var r in Rates(content).OrderBy(r => r.Unlock).ThenBy(r => r.Kind))
                sb.AppendLine(string.Format(ci, "| {0} | {1} | {2} | {3:0.#} | {4:0.00} | {5:0.00} |", r.Name, r.Kind, r.Unlock, r.Minutes, r.CoinsPerMinute, r.XpPerMinute));

            sb.AppendLine();
            sb.AppendLine(string.Format(ci, "## Level pacing (efficiency {0:0%}, orders ignored)", Efficiency));
            sb.AppendLine();
            var materialNames = content.Materials.Where(m => m != null).Select(m => m.DisplayName).ToList();
            sb.AppendLine($"| Level | New key buildings, first land plots (cost) | Coins after buying | Fields | XP to next | Coins/min (of which train) | XP/min | Minutes to next | Coins earned | Coins at level end | Materials earned ({string.Join(" / ", materialNames)}) |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var row in Levels(content))
            {
                string costs = row.BuildingCosts > 0 ? $"{row.NewBuildings} ({row.BuildingCosts:N0})" : "-";
                string after = row.BalanceAfterBuying < 0 ? $"**{row.BalanceAfterBuying:N0}**" : row.BalanceAfterBuying.ToString("N0", ci);
                string cpm = row.TrainCoinsPerMinute > 0 ? string.Format(ci, "{0:0.0} ({1:0.0})", row.CoinsPerMinute, row.TrainCoinsPerMinute)
                    : string.Format(ci, "{0:0.0}", row.CoinsPerMinute);
                string mats = string.Join(" / ", content.Materials.Where(m => m != null)
                    .Select(m => (row.MaterialsEarned.TryGetValue(m.Id, out var v) ? v : 0f).ToString("0.0", ci)));
                sb.AppendLine(string.Format(ci, "| {0} | {1} | {2} | {3} | {4:N0} | {5} | {6:0.00} | {7:0} | {8:N0} | {9:N0} | {10} |",
                    row.Level, costs, after, row.Fields, row.XpToNext, cpm, row.XpPerMinute, row.Minutes, row.Earned, row.BalanceAtEnd, mats));
            }

            sb.AppendLine();
            sb.AppendLine("## Land: when the first plot of each ring is affordable");
            sb.AppendLine();
            sb.AppendLine("| Ring | Unlocks at level | Price | Materials earned by level |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var l in Land(content))
                sb.AppendLine(string.Format(ci, "| {0} | {1} | {2:N0} coins + {3} | {4} |", l.Ring, l.UnlockLevel, l.Coins, l.Materials,
                    l.MaterialsReadyLevel >= 0 ? l.MaterialsReadyLevel.ToString(ci) : $"after {ReportLevels}"));

            string path = Path.Combine(Directory.GetCurrentDirectory(), "Documentation", "EconomyReport.md");
            File.WriteAllText(path, sb.ToString().Replace("\r\n", "\n"));
            return path;
        }
    }
}
