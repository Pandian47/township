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
    /// <summary>Milestone 7 step 2: achievements end to end.</summary>
    public sealed class AchievementsFlowTests
    {
        private string _saveDir;
        private ManualClock _clock;

        [SetUp]
        public void SetUp()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            _saveDir = Path.Combine(Path.GetTempPath(), "TownBuilderAchievementTests", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator NewTown_HasSevenAchievements_WithGoalsButton()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            Assert.AreEqual(7, content.Achievements.Count);
            Assert.AreEqual(7, scene.Achievements.Book.Specs.Count);
            Assert.AreEqual(1, scene.Achievements.Book.Count(AchievementKind.ReachLevel), "level counter starts at the current level");

            var panel = Object.FindAnyObjectByType<AchievementsPanel>();
            Assert.IsNotNull(panel);
            Assert.IsFalse(panel.IsOpen);
            panel.Open();
            Assert.IsTrue(panel.IsOpen);
        }

        [UnityTest]
        public IEnumerator LevelingUp_MakesTierClaimable_ClaimPaysGems_AndIsSaved()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            content.TryGetAchievement("reach_level", out var def);
            int ready = 0;
            scene.Achievements.Ready += _ => ready++;

            while (scene.Progress.Level < def.Tiers[0].goal) scene.Progress.AddXp(10);
            Assert.AreEqual(1, ready, "one toast when the goal is reached");
            Assert.IsTrue(scene.Achievements.Book.CanClaim("reach_level"));

            long gems = scene.Wallet.Gems;
            Assert.IsTrue(scene.Achievements.Claim("reach_level"));
            Assert.AreEqual(gems + def.Tiers[0].gems, scene.Wallet.Gems);
            Assert.IsFalse(scene.Achievements.Claim("reach_level"), "next tier not reached");

            // Counters from events.
            scene.Achievements.Book.Add(AchievementKind.HarvestCrops, 7);

            scene.SaveNow();
            Object.DestroyImmediate(GameRoot.Current.gameObject);
            yield return null;
            SceneManager.LoadScene(SceneNames.MainMenu);
            float end = Time.realtimeSinceStartup + 5f;
            while (GameRoot.Current == null && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(GameRoot.Current.Context.ContinueGame().Succeeded);
            yield return LoadTown();
            scene = Object.FindAnyObjectByType<TownSceneController>();
            Assert.AreEqual(1, scene.Achievements.Book.ClaimedTiers("reach_level"));
            Assert.AreEqual(7, scene.Achievements.Book.Count(AchievementKind.HarvestCrops));
        }

        [UnityTest]
        public IEnumerator Harvesting_CountsCrops()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            content.TryGetCrop("wheat", out var wheat);
            var fieldId = scene.Town.Buildings.First(b => b.DefinitionId == "field").InstanceId;
            long before = scene.Achievements.Book.Count(AchievementKind.HarvestCrops);

            Assert.AreEqual(PlantResult.Planted, scene.Farming.Plant(fieldId, wheat));
            _clock.Advance(60 * 60 * 1000);
            Assert.IsTrue(scene.Farming.TryHarvest(fieldId));
            Assert.AreEqual(before + wheat.HarvestYield, scene.Achievements.Book.Count(AchievementKind.HarvestCrops));
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
