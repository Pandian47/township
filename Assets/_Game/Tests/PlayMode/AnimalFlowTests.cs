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
    /// <summary>Milestone 4 step 2: Chicken Coop and Dairy Shed turn feed into eggs and milk, also while the app is closed.</summary>
    public sealed class AnimalFlowTests
    {
        private string _saveDir;
        private ManualClock _clock;

        [SetUp]
        public void SetUp()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            _saveDir = Path.Combine(Path.GetTempPath(), "TownBuilderAnimalTests", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator Coop_And_Dairy_ProduceWhileAppIsClosed()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            content.TryGetBuilding("chicken_coop", out var coopDef);
            content.TryGetBuilding("dairy_shed", out var dairyDef);
            content.TryGetRecipe("collect_eggs", out var eggs);
            content.TryGetRecipe("collect_milk", out var milk);

            Assert.IsFalse(scene.Placement.BeginPlacement(coopDef), "Chicken Coop is locked at level 1");

            scene.Progress.AddXp(10_000);
            Assert.GreaterOrEqual(scene.Progress.Level, dairyDef.UnlockLevel);
            scene.Wallet.AddCoins(10_000);

            string coop = Build(scene, coopDef);
            string dairy = Build(scene, dairyDef);

            scene.Inventory.TryAdd("chicken_feed", 2);
            scene.Inventory.TryAdd("cow_feed", 1);
            Assert.AreEqual(QueueResult.Queued, scene.Production.Enqueue(coop, eggs));
            Assert.AreEqual(QueueResult.Queued, scene.Production.Enqueue(coop, eggs));
            Assert.AreEqual(QueueResult.Queued, scene.Production.Enqueue(dairy, milk));
            Assert.AreEqual(QueueResult.MissingIngredients, scene.Production.Enqueue(dairy, milk), "feed is used up");
            Assert.AreEqual(0, scene.Inventory.Count("chicken_feed"));

            // Close the app until everything is done, then reopen.
            scene.SaveNow();
            Object.DestroyImmediate(GameRoot.Current.gameObject);
            _clock.Advance(2 * eggs.DurationMs + milk.DurationMs);
            yield return null;
            SceneManager.LoadScene(SceneNames.MainMenu);
            float end = Time.realtimeSinceStartup + 5f;
            while (GameRoot.Current == null && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(GameRoot.Current.Context.ContinueGame().Succeeded);
            yield return LoadTown();
            scene = Object.FindAnyObjectByType<TownSceneController>();

            Assert.AreEqual(2, scene.Production.Collect(coop));
            Assert.AreEqual(1, scene.Production.Collect(dairy));
            Assert.AreEqual(2 * eggs.OutputAmount, scene.Inventory.Count("egg"));
            Assert.AreEqual(milk.OutputAmount, scene.Inventory.Count("milk"));
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
