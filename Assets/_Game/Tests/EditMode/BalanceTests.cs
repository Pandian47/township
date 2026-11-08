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
        public void NoRecipe_IsMoreThan2xBetter_ThanTheAverage()
        {
            var recipes = EconomyReport.Rates(_content).Where(r => !r.Kind.StartsWith("Crop")).ToList();
            float average = recipes.Average(r => r.CoinsPerMinute);
            foreach (var r in recipes)
                Assert.LessOrEqual(r.CoinsPerMinute, average * 2f, $"{r.Name} earns {r.CoinsPerMinute:0.0}/min vs average {average:0.0}");
        }
    }
}
