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
    /// * The player buys one of each new factory/storage building when it unlocks. Cottages and decorations are optional.
    ///
    /// Menu: TownBuilder > Debug > Write Economy Report (writes Documentation/EconomyReport.md).
    /// </summary>
    public static class EconomyReport
    {
        public const float Efficiency = 0.5f;
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
            public long Earned;
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

        public static List<LevelRow> Levels(GameContent content)
        {
            var curve = content.CreateLevelCurve();
            var rows = new List<LevelRow>();
            long coins = content.StartingCoins;
            var keyBuildings = content.Buildings.Where(b => b.IsFactory || b.StorageCapacityBonus > 0).ToList();

            for (int level = 1; level <= ReportLevels; level++)
            {
                var row = new LevelRow { Level = level, XpToNext = curve.XpToNext(level), Fields = Math.Min(12, 2 + level) };
                row.LevelUpReward = level > 1 ? (long)content.LevelUpCoinsPerLevel * level : 0;
                coins += row.LevelUpReward;

                var newOnes = keyBuildings.Where(b => b.UnlockLevel == level).ToList();
                row.BuildingCosts = newOnes.Sum(b => (long)b.CoinCost);
                row.NewBuildings = newOnes.Count > 0 ? string.Join(", ", newOnes.Select(b => b.DisplayName)) : "-";
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

                row.CoinsPerMinute = cpm * Efficiency;
                row.XpPerMinute = xpm * Efficiency;
                row.Minutes = row.XpPerMinute > 0 ? row.XpToNext / row.XpPerMinute : float.PositiveInfinity;
                row.Earned = (long)(row.CoinsPerMinute * row.Minutes);
                coins += row.Earned;
                row.BalanceAtEnd = coins;
                rows.Add(row);
            }

            return rows;
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
            sb.AppendLine("| Level | New key buildings (cost) | Coins after buying | Fields | XP to next | Coins/min | XP/min | Minutes to next | Coins earned | Coins at level end |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
            foreach (var row in Levels(content))
            {
                string costs = row.BuildingCosts > 0 ? $"{row.NewBuildings} ({row.BuildingCosts:N0})" : "-";
                string after = row.BalanceAfterBuying < 0 ? $"**{row.BalanceAfterBuying:N0}**" : row.BalanceAfterBuying.ToString("N0", ci);
                sb.AppendLine(string.Format(ci, "| {0} | {1} | {2} | {3} | {4:N0} | {5:0.0} | {6:0.00} | {7:0} | {8:N0} | {9:N0} |",
                    row.Level, costs, after, row.Fields, row.XpToNext, row.CoinsPerMinute, row.XpPerMinute, row.Minutes, row.Earned, row.BalanceAtEnd));
            }

            string path = Path.Combine(Directory.GetCurrentDirectory(), "Documentation", "EconomyReport.md");
            File.WriteAllText(path, sb.ToString().Replace("\r\n", "\n"));
            return path;
        }
    }
}
