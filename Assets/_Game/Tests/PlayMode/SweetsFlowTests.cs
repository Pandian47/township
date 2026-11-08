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
    /// <summary>Milestone 4 step 4: Bakery cookies and cake, Creamery butter and cheese.</summary>
    public sealed class SweetsFlowTests
    {
        private string _saveDir;
        private ManualClock _clock;

        [SetUp]
        public void SetUp()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            _saveDir = Path.Combine(Path.GetTempPath(), "TownBuilderSweetsTests", System.Guid.NewGuid().ToString("N"));
            _clock = new ManualClock(1_700_000_000_000);
            GameRoot.SaveDirectoryOverride = _saveDir;
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
        public IEnumerator Bakery_And_Creamery_MakeSweetsAndDairy()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            content.TryGetBuilding("bakery", out var bakeryDef);
            content.TryGetBuilding("creamery", out var creameryDef);
            content.TryGetRecipe("bake_cookies", out var cookies);
            content.TryGetRecipe("bake_cake", out var cake);
            content.TryGetRecipe("churn_butter", out var butter);
            content.TryGetRecipe("make_cheese", out var cheese);

            scene.Wallet.AddCoins(10_000);
            string bakery = Build(scene, bakeryDef);
            scene.Inventory.TryAdd("wheat", 4);
            scene.Inventory.TryAdd("sugar", 2);
            Assert.AreEqual(QueueResult.Locked, scene.Production.Enqueue(bakery, cookies), "cookies need level 8");

            scene.Progress.AddXp(20_000);
            Assert.GreaterOrEqual(scene.Progress.Level, cake.UnlockLevel);
            string creamery = Build(scene, creameryDef);

            scene.Inventory.TryAdd("egg", 2);
            scene.Inventory.TryAdd("milk", 4);
            Assert.AreEqual(QueueResult.Queued, scene.Production.Enqueue(bakery, cookies));
            Assert.AreEqual(QueueResult.Queued, scene.Production.Enqueue(bakery, cake));
            Assert.AreEqual(QueueResult.Queued, scene.Production.Enqueue(creamery, butter));
            Assert.AreEqual(QueueResult.Queued, scene.Production.Enqueue(creamery, cheese));
            Assert.AreEqual(0, scene.Inventory.Count("wheat"));
            Assert.AreEqual(0, scene.Inventory.Count("sugar"));
            Assert.AreEqual(0, scene.Inventory.Count("egg"));
            Assert.AreEqual(0, scene.Inventory.Count("milk"));

            _clock.Advance(cookies.DurationMs + cake.DurationMs + butter.DurationMs + cheese.DurationMs);
            Assert.AreEqual(2, scene.Production.Collect(bakery));
            Assert.AreEqual(2, scene.Production.Collect(creamery));
            foreach (var id in new[] { "cookies", "cake", "butter", "cheese" })
                Assert.AreEqual(1, scene.Inventory.Count(id), id);
        }

        private static string Build(TownSceneController scene, BuildingDefinition def)
        {
            Assert.IsTrue(scene.Placement.BeginPlacement(def), $"place {def.Id}");
            Assert.IsTrue(scene.Placement.Confirm(), $"confirm {def.Id}");
            string id = scene.Town.Buildings.Last(b => b.DefinitionId == def.Id).InstanceId;
            Assert.IsTrue(scene.Construction.FinishNow(id));
            return id;
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
