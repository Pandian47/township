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
    /// <summary>Milestone 6 step 1: ambient life (swaying crops, chimney smoke, bobbing badges).</summary>
    public sealed class AmbientFlowTests
    {
        private string _saveDir;
        private ManualClock _clock;

        [SetUp]
        public void SetUp()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            _saveDir = Path.Combine(Path.GetTempPath(), "TownBuilderAmbientTests", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator Crops_UseTheSwayMaterial()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            GameRoot.Current.Context.Content.TryGetCrop("wheat", out var wheat);
            string field = scene.Town.Buildings.First(b => b.DefinitionId == "field").InstanceId;
            scene.Farming.Plant(field, wheat);
            yield return null;

            var view = Object.FindObjectsByType<BuildingView>().First(v => v.InstanceId == field);
            var crop = view.transform.Find("Crop").GetComponent<SpriteRenderer>();
            Assert.NotNull(crop.sharedMaterial);
            Assert.AreEqual("TownBuilder/SpriteSway", crop.sharedMaterial.shader.name);
        }

        [UnityTest]
        public IEnumerator WorkingFactory_Smokes_IdleOneDoesNot_AndReadyBadgeBobs()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            content.TryGetBuilding("bakery", out var bakeryDef);
            content.TryGetRecipe("bake_bread", out var bread);
            Assert.IsTrue(bakeryDef.HasChimney, "the Bakery art has a chimney");
            var smoke = Object.FindAnyObjectByType<ChimneySmoke>();
            Assert.NotNull(smoke, "smoke emitter in the scene");

            scene.Wallet.AddCoins(5_000);
            scene.Placement.BeginPlacement(bakeryDef);
            scene.Placement.Confirm();
            string bakery = scene.Town.Buildings.First(b => b.DefinitionId == "bakery").InstanceId;
            scene.Construction.FinishNow(bakery);

            yield return new WaitForSecondsRealtime(1.2f);
            Assert.IsFalse(smoke.WorkingFactories.Contains(bakery), "idle bakery: no smoke");

            scene.Inventory.TryAdd("wheat", 2);
            Assert.AreEqual(QueueResult.Queued, scene.Production.Enqueue(bakery, bread));
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.IsTrue(smoke.WorkingFactories.Contains(bakery), "baking: smoke");
            Assert.Greater(smoke.ActivePuffs, 0);

            // Finished: smoke stops, the "ready" badge appears and floats.
            _clock.Advance(bread.DurationMs);
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.IsFalse(smoke.WorkingFactories.Contains(bakery), "done: no more smoke");
            var view = Object.FindObjectsByType<BuildingView>().First(v => v.InstanceId == bakery);
            var badge = view.transform.Find("ReadyIcon");
            Assert.NotNull(badge, "ready badge");
            Assert.NotNull(badge.GetComponent<FloatBob>(), "and it bobs");
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
