using System.Linq;
using NUnit.Framework;
using TownBuilder.EditorTools;
using UnityEditor;

namespace TownBuilder.Tests
{
    /// <summary>
    /// Guard rails from the balance model (EconomyReport). If a content change breaks one of these, regenerate
    /// Documentation/EconomyReport.md and rebalance (costs, rewards, timers) before shipping.
    /// </summary>
    public sealed class BalanceTests
    {
        private GameContent _content;

        [SetUp]
        public void SetUp() => _content = AssetDatabase.LoadAssetAtPath<GameContent>("Assets/_Game/ScriptableObjects/GameContent.asset");

        [Test]
        public void PlayerCanAfford_EachNewKeyBuilding_WhenItUnlocks()
        {
            foreach (var row in EconomyReport.Levels(_content))
                Assert.GreaterOrEqual(row.BalanceAfterBuying, 0,
                    $"Level {row.Level}: {row.NewBuildings} costs {row.BuildingCosts}, coins after buying {row.BalanceAfterBuying}");
        }

        [Test]
        public void FirstPlotOfEachRing_MaterialsEarned_WithinOneLevelOfUnlocking()
        {
            foreach (var l in EconomyReport.Land(_content))
            {
                Assert.GreaterOrEqual(l.MaterialsReadyLevel, 0, $"ring {l.Ring}: materials never earned within the report levels");
                Assert.LessOrEqual(l.MaterialsReadyLevel, l.UnlockLevel + 1,
                    $"ring {l.Ring} unlocks at level {l.UnlockLevel} but its materials only arrive at level {l.MaterialsReadyLevel}");
            }
        }

        [Test]
        public void EveryMaterial_HasASource_BeforeTheFirstLandRing()
        {
            int firstRing = _content.LandTiers.Min(t => t.unlockLevel);
            var rates = EconomyReport.MaterialRates(_content, firstRing);
            foreach (var cost in _content.LandTiers.First(t => t.unlockLevel == firstRing).materials)
                Assert.Greater(rates[cost.material.Id], 0f, $"{cost.material.Id} can't be earned at level {firstRing}");
        }

        [Test]
        public void Levels_TakeBetween5MinutesAnd2Hours_OfActivePlay()
        {
            foreach (var row in EconomyReport.Levels(_content))
            {
                Assert.GreaterOrEqual(row.Minutes, 5f, $"Level {row.Level} is too quick ({row.Minutes:0} min)");
                Assert.LessOrEqual(row.Minutes, 120f, $"Level {row.Level} is too slow ({row.Minutes:0} min)");
            }
        }

        [Test]
        public void EveryCropAndRecipe_EarnsSomething()
        {
            foreach (var rate in EconomyReport.Rates(_content))
            {
                Assert.Greater(rate.CoinsPerMinute, 0f, $"{rate.Name} ({rate.Kind}) earns no coins");
                Assert.Greater(rate.XpPerMinute, 0f, $"{rate.Name} ({rate.Kind}) gives no XP");
            }
        }

        [Test]
        public void BonusCoins_DailyAndEvents_StayABonus()
        {
            // After the first minutes, daily rewards + weekend events add at most a quarter on top of normal play.
            foreach (var row in EconomyReport.Levels(_content).Where(r => r.Level >= 2))
                Assert.LessOrEqual(row.BonusShare, 0.25f,
                    $"Level {row.Level}: bonus coins are {row.BonusShare:0%} of normal income so far (daily reward or event prizes too big)");
        }

        [Test]
        public void Gems_NeverShort_ForSkippingEveryBuild()
        {
            foreach (var row in EconomyReport.Levels(_content))
                Assert.GreaterOrEqual(row.GemsTotal, row.GemsToSkipBuildsTotal,
                    $"Level {row.Level}: {row.GemsTotal} free gems but {row.GemsToSkipBuildsTotal} needed to skip every build");
        }

        [Test]
        public void Goals_FirstTierReached_WithinThreeLevels()
        {
            Assert.IsTrue(EconomyReport.Levels(_content).Where(r => r.Level <= 3).Any(r => r.GoalsReached != "-"),
                "no Goal tier is reached in the first three levels: the Goals screen would feel dead");
        }

        [Test]
        public void WeekendEvents_AllPrizes_ReachableInOneWeekendOfPlay_AtLevel3()
        {
            var row = EconomyReport.Levels(_content).First(r => r.Level == 3);
            float weekend = 2 * EconomyReport.PlayMinutesPerDay;
            foreach (var ev in _content.WeekendEvents.Where(e => e != null && e.Milestones.Count > 0))
            {
                float perMinute = ev.PointsFrom == TownBuilder.Domain.AchievementKind.HarvestCrops ? row.CropsPerMinute
                    : ev.PointsFrom == TownBuilder.Domain.AchievementKind.MakeProducts ? row.ProductsPerMinute : -1f;
                if (perMinute < 0f) continue; // orders aren't in the model
                float minutes = ev.Milestones.Last().goal / perMinute;
                Assert.LessOrEqual(minutes, weekend, $"{ev.DisplayName}: the last prize takes {minutes:0} min of play at level 3");
            }
        }

        [Test]
        public void NoRecipe_IsMoreThan2xBetter_ThanTheAverage()
        {
            var recipes = EconomyReport.Rates(_content).Where(r => !r.Kind.StartsWith("Crop")).ToList();
            float average = recipes.Average(r => r.CoinsPerMinute);
            foreach (var r in recipes)
                Assert.LessOrEqual(r.CoinsPerMinute, average * 2f, $"{r.Name} earns {r.CoinsPerMinute:0.0}/min vs average {average:0.0}");
        }
    }
}
