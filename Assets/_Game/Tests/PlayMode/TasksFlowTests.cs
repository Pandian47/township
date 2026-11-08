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
    /// <summary>Milestone 5 step 1: town tasks from game events, claiming rewards, persistence.</summary>
    public sealed class TasksFlowTests
    {
        private string _saveDir;
        private ManualClock _clock;

        [SetUp]
        public void SetUp()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            _saveDir = Path.Combine(Path.GetTempPath(), "TownBuilderTasksTests", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator NewTown_StartsWithGuideTasks_InOrder()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var board = scene.Tasks.Board;
            Assert.AreEqual(GameRoot.Current.Context.Content.TaskSlots, board.SlotCount);
            Assert.AreEqual("guide_harvest_wheat", board.Get(0).Spec.Id);
            Assert.AreEqual("guide_build_bakery", board.Get(1).Spec.Id);
            Assert.AreEqual("guide_bake_bread", board.Get(2).Spec.Id);

            yield return null;
            var panel = GameObject.Find("TasksPanel");
            Assert.NotNull(panel, "tasks panel in the HUD");
            Assert.IsTrue(panel.transform.Find("Root").gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator Harvesting_CompletesTask_ClaimPaysAndRefills_AndSurvivesRestart()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            content.TryGetCrop("wheat", out var wheat);
            content.TryGetTask("guide_harvest_wheat", out var task);
            var fields = scene.Town.Buildings.Where(b => b.DefinitionId == "field").Select(b => b.InstanceId).ToList();

            // Harvest until the wheat task is done.
            int harvested = 0;
            while (harvested < task.Amount)
            {
                foreach (var field in fields)
                {
                    scene.Farming.Plant(field, wheat);
                    _clock.Advance(wheat.GrowDurationMs);
                    Assert.IsTrue(scene.Farming.TryHarvest(field));
                    harvested += wheat.HarvestYield;
                    if (harvested >= task.Amount) break;
                }
            }

            Assert.IsTrue(scene.Tasks.Board.Get(0).IsComplete);

            long coins = scene.Wallet.Coins;
            Assert.IsTrue(scene.Tasks.Claim(0));
            Assert.AreEqual(coins + task.RewardCoins, scene.Wallet.Coins);
            Assert.IsFalse(scene.Tasks.Claim(0), "the new task isn't complete yet");
            Assert.AreNotEqual("guide_harvest_wheat", scene.Tasks.Board.Get(0)?.Spec.Id, "slot refilled with another task");

            // Building a Bakery advances the build task.
            content.TryGetBuilding("bakery", out var bakery);
            Assert.IsTrue(scene.Placement.BeginPlacement(bakery));
            Assert.IsTrue(scene.Placement.Confirm());
            Assert.IsTrue(scene.Tasks.Board.Slots.Any(t => t != null && t.Spec.Id == "guide_build_bakery" && t.IsComplete));

            // Restart: claimed guide task stays claimed, completed build task stays complete.
            scene.SaveNow();
            Object.DestroyImmediate(GameRoot.Current.gameObject);
            yield return null;
            SceneManager.LoadScene(SceneNames.MainMenu);
            float end = Time.realtimeSinceStartup + 5f;
            while (GameRoot.Current == null && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(GameRoot.Current.Context.ContinueGame().Succeeded);
            yield return LoadTown();
            scene = Object.FindAnyObjectByType<TownSceneController>();

            var board = scene.Tasks.Board;
            CollectionAssert.Contains(board.Completed.ToList(), "guide_harvest_wheat");
            Assert.IsFalse(board.Slots.Any(t => t != null && t.Spec.Id == "guide_harvest_wheat"));
            Assert.IsTrue(board.Slots.Any(t => t != null && t.Spec.Id == "guide_build_bakery" && t.IsComplete));
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
