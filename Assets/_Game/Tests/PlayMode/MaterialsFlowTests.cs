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
    /// <summary>Milestone 5 step 3: building materials from train crates and tasks.</summary>
    public sealed class MaterialsFlowTests
    {
        private string _saveDir;
        private ManualClock _clock;

        [SetUp]
        public void SetUp()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            _saveDir = Path.Combine(Path.GetTempPath(), "TownBuilderMaterialsTests", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator TrainCrateWithMaterial_PaysTheMaterial_AndItIsSaved()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            content.TryGetBuilding(content.TrainStationId, out var stationDef);
            scene.Progress.AddXp(10_000);
            scene.Wallet.AddCoins(10_000);
            scene.Placement.BeginPlacement(stationDef);
            scene.Placement.Confirm();
            string station = scene.Town.Buildings.First(b => b.DefinitionId == stationDef.Id).InstanceId;
            scene.Construction.FinishNow(station);
            yield return new WaitForSecondsRealtime(0.3f);

            // A known crate that also pays 1 Planks.
            scene.Train.Train.Restore(TrainState.AtStation, 0, new[] { new TrainCrate("wheat", 3, 9, 1, false, "planks", 1) });
            scene.Inventory.TryAdd("wheat", 3);
            Assert.AreEqual(CrateFillResult.Filled, scene.Train.Fill(0));
            Assert.AreEqual(1, scene.Inventory.Count("planks"));
            Assert.IsTrue(scene.Effects.FloatingText.ActiveTexts().Contains("+1 Planks"), "material floats above the station");

            scene.SaveNow();
            Object.DestroyImmediate(GameRoot.Current.gameObject);
            yield return null;
            SceneManager.LoadScene(SceneNames.MainMenu);
            float end = Time.realtimeSinceStartup + 5f;
            while (GameRoot.Current == null && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(GameRoot.Current.Context.ContinueGame().Succeeded);
            yield return LoadTown();
            scene = Object.FindAnyObjectByType<TownSceneController>();
            Assert.AreEqual(1, scene.Inventory.Count("planks"), "materials are saved in the barn");
        }

        [UnityTest]
        public IEnumerator TaskWithMaterialReward_PaysIt_ButNotIntoAFullBarn()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            Assert.IsTrue(content.TryGetTask("orders_for_planks", out var task));
            Assert.Greater(task.RewardMaterialAmount, 0);

            // Put the finished task on the board.
            scene.Tasks.Board.Restore(0, task.ToSpec(), task.Amount);

            // Full barn: claim waits.
            scene.Inventory.TryAdd("wheat", scene.Inventory.FreeSpace);
            Assert.IsFalse(scene.Tasks.Claim(0), "no room for the planks");
            Assert.IsTrue(scene.Tasks.Board.Get(0).IsComplete, "task stays claimable");

            scene.Inventory.TryRemove("wheat", 5);
            long coins = scene.Wallet.Coins;
            Assert.IsTrue(scene.Tasks.Claim(0));
            Assert.AreEqual(task.RewardMaterialAmount, scene.Inventory.Count("planks"));
            Assert.AreEqual(coins + task.RewardCoins, scene.Wallet.Coins);
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
