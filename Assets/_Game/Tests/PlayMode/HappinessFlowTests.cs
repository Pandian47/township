using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TownBuilder.Domain;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TownBuilder.Tests
{
    /// <summary>Milestone 8 step 2: homes earn coins, decorations nearby make them happier and earn more.</summary>
    public sealed class HappinessFlowTests
    {
        private const long Hour = 3_600_000L;
        private string _saveDir;
        private ManualClock _clock;

        [SetUp]
        public void SetUp()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            _saveDir = Path.Combine(Path.GetTempPath(), "TownBuilderHappinessTests", System.Guid.NewGuid().ToString("N"));
            GameRoot.SaveDirectoryOverride = _saveDir;
            _clock = new ManualClock(1_700_000_000_000);
            GameRoot.ClockOverride = _clock;
        }

        [TearDown]
        public void TearDown()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            GameRoot.SaveDirectoryOverride = null;
            GameRoot.ClockOverride = null;
            if (Directory.Exists(_saveDir)) Directory.Delete(_saveDir, true);
        }

        [UnityTest]
        public IEnumerator Home_EarnsCoins_MoreWhenDecorationsAreNear_AndIsSaved()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            content.TryGetBuilding("cottage", out var cottageDef);
            content.TryGetBuilding("fountain", out var fountainDef);
            Assert.Greater(fountainDef.Happiness, 0, "decorations have happiness");
            scene.Wallet.AddCoins(10_000);
            scene.Progress.AddXp(10_000);

            // Build a cottage; its coins start filling once it's finished.
            Assert.IsTrue(scene.Placement.BeginPlacement(cottageDef));
            Assert.IsTrue(scene.Placement.Confirm());
            string home = scene.Town.Buildings.Last(b => b.DefinitionId == "cottage").InstanceId;
            scene.Construction.FinishNow(home);
            yield return new WaitForSeconds(1.2f); // controller tick starts tracking the home
            Assert.IsTrue(scene.Homes.IsHome(home, out _));
            int before = scene.Homes.Happiness(home);

            // A fountain right next to it.
            var cottage = scene.Town.Buildings.First(b => b.InstanceId == home);
            var spot = FreeSpotNear(scene, cottage.Origin, fountainDef.FootprintSize);
            Assert.IsTrue(spot.HasValue, "room for a fountain next to the cottage");
            Assert.AreEqual(PlacementResult.Valid, scene.Town.TryAdd("test_fountain", "fountain", spot.Value, fountainDef.FootprintSize, out _));
            Assert.AreEqual(before + fountainDef.Happiness, scene.Homes.Happiness(home));
            double rate = scene.Homes.CoinsPerHour(home);
            Assert.AreEqual(TownHappiness.CoinsPerHour(content.HomeCoinsPerHour, scene.Homes.Happiness(home)), rate, 1e-6);

            // Two hours later: coins waiting, bubble shown; collecting pays and empties it.
            _clock.Advance(2 * Hour);
            yield return new WaitForSeconds(1.2f);
            int pending = scene.Homes.Pending(home);
            Assert.AreEqual((int)System.Math.Floor(2 * rate), pending);
            Assert.IsTrue(scene.Homes.BubbleVisible(home));
            long coins = scene.Wallet.Coins;
            Assert.AreEqual(pending, scene.Homes.Collect(home));
            Assert.AreEqual(coins + pending, scene.Wallet.Coins);
            Assert.AreEqual(0, scene.Homes.Pending(home));

            // Capped: a long absence fills only up to the cap. Saved across a restart.
            _clock.Advance(24 * Hour);
            scene.SaveNow();
            Object.DestroyImmediate(GameRoot.Current.gameObject);
            yield return null;
            SceneManager.LoadScene(SceneNames.MainMenu);
            float end = Time.realtimeSinceStartup + 5f;
            while (GameRoot.Current == null && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(GameRoot.Current.Context.ContinueGame().Succeeded);
            yield return LoadTown();
            scene = Object.FindAnyObjectByType<TownSceneController>();
            Assert.AreEqual((int)System.Math.Floor(content.HomeCoinsMaxHours * scene.Homes.CoinsPerHour(home)), scene.Homes.Pending(home),
                "a full home after a long time away");
        }

        private static GridCoord? FreeSpotNear(TownSceneController scene, GridCoord origin, GridCoord size)
        {
            for (int r = 1; r <= 3; r++)
            for (int dx = -r - size.X; dx <= r + 2; dx++)
            for (int dy = -r - size.Y; dy <= r + 2; dy++)
            {
                var c = new GridCoord(origin.X + dx, origin.Y + dy);
                if (scene.Town.CanPlace(c, size) == PlacementResult.Valid) return c;
            }

            return null;
        }

        private static IEnumerator LoadTown()
        {
            SceneManager.LoadScene(SceneNames.Town);
            float end = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < end)
            {
                var c = Object.FindAnyObjectByType<TownSceneController>();
                if (c != null && c.IsReady) yield break;
                yield return null;
            }

            Assert.Fail("Town scene did not initialise");
        }
    }
}
