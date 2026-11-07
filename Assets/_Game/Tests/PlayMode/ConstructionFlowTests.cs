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
    /// <summary>Construction timers in the real Town scene.</summary>
    public sealed class ConstructionFlowTests
    {
        private string _saveDir;
        private ManualClock _clock;

        [SetUp]
        public void SetUp()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            _saveDir = Path.Combine(Path.GetTempPath(), "TownBuilderBuildTests", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator NewBakery_IsUnderConstruction_ThenWorks_AndGivesXp()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            content.TryGetBuilding("bakery", out var bakery);
            content.TryGetRecipe("bake_bread", out var bread);

            scene.Placement.BeginPlacement(bakery);
            scene.Placement.Confirm();
            string id = scene.Town.Buildings.First(b => b.DefinitionId == "bakery").InstanceId;
            scene.Inventory.TryAdd("wheat", 2);

            Assert.IsFalse(scene.Construction.IsBuilt(id));
            Assert.AreEqual(QueueResult.NotAFactory, scene.Production.Enqueue(id, bread), "can't produce while building");

            long xp = scene.Progress.Xp;
            _clock.Advance(bakery.BuildDurationMs);
            yield return new WaitForSecondsRealtime(0.4f); // controller checks every 0.25 s

            Assert.IsTrue(scene.Construction.IsBuilt(id));
            Assert.AreEqual(xp + bakery.BuildXp, scene.Progress.Xp);
            Assert.AreEqual(QueueResult.Queued, scene.Production.Enqueue(id, bread));
        }

        [UnityTest]
        public IEnumerator FinishWithGems_SpendsGems()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            GameRoot.Current.Context.Content.TryGetBuilding("barn", out var barn);
            scene.Placement.BeginPlacement(barn);
            scene.Placement.Confirm();
            string id = scene.Town.Buildings.First(b => b.DefinitionId == "barn").InstanceId;

            long gems = scene.Wallet.Gems;
            int cost = scene.Construction.GemCostToFinish(id);
            Assert.Greater(cost, 0);
            Assert.IsTrue(scene.Construction.FinishWithGems(id));
            Assert.AreEqual(gems - cost, scene.Wallet.Gems);
            Assert.IsTrue(scene.Construction.IsBuilt(id));
        }

        [UnityTest]
        public IEnumerator Construction_ContinuesWhileAppClosed()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            GameRoot.Current.Context.Content.TryGetBuilding("cottage", out var cottage);
            scene.Placement.BeginPlacement(cottage);
            scene.Placement.Confirm();
            string id = scene.Town.Buildings.First(b => b.DefinitionId == "cottage").InstanceId;
            Assert.IsFalse(scene.Construction.IsBuilt(id));

            // Save, reopen 5 s later: still building.
            scene.SaveNow();
            yield return Restart(5000);
            scene = Object.FindAnyObjectByType<TownSceneController>();
            Assert.IsFalse(scene.Construction.IsBuilt(id), "construction state is saved");

            // Reopen after the build time: done on load.
            scene.SaveNow();
            yield return Restart(cottage.BuildDurationMs);
            scene = Object.FindAnyObjectByType<TownSceneController>();
            Assert.IsTrue(scene.Construction.IsBuilt(id));
        }

        private IEnumerator Restart(long advanceMs)
        {
            Object.DestroyImmediate(GameRoot.Current.gameObject);
            _clock.Advance(advanceMs);
            yield return null;
            SceneManager.LoadScene(SceneNames.MainMenu);
            yield return WaitFor(() => GameRoot.Current != null);
            Assert.IsTrue(GameRoot.Current.Context.ContinueGame().Succeeded);
            SceneManager.LoadScene(SceneNames.Town);
            yield return WaitForTown();
        }

        private static IEnumerator LoadTown()
        {
            SceneManager.LoadScene(SceneNames.Town);
            yield return WaitForTown();
        }

        private static IEnumerator WaitForTown()
        {
            yield return WaitFor(() =>
            {
                var c = Object.FindAnyObjectByType<TownSceneController>();
                return c != null && c.IsReady;
            });
            Assert.IsTrue(Object.FindAnyObjectByType<TownSceneController>().IsReady);
        }

        private static IEnumerator WaitFor(System.Func<bool> condition, float timeoutSeconds = 5f)
        {
            float end = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        }
    }
}
