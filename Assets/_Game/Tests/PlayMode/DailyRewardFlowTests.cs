using System.Collections;
using System.IO;
using NUnit.Framework;
using TownBuilder.Domain;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TownBuilder.Tests
{
    /// <summary>Milestone 7 step 1: daily reward end to end.</summary>
    public sealed class DailyRewardFlowTests
    {
        private const long DayMs = 86_400_000L;
        private string _saveDir;
        private ManualClock _clock;

        [SetUp]
        public void SetUp()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            _saveDir = Path.Combine(Path.GetTempPath(), "TownBuilderDailyTests", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator NewTown_OpensCalendar_ClaimPaysDay1_OncePerDay()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var daily = scene.Daily;
            var content = GameRoot.Current.Context.Content;
            var dialog = Object.FindAnyObjectByType<DailyRewardDialog>();
            Assert.AreEqual(7, content.DailyRewards.Count, "a 7-day calendar is set up");
            Assert.IsTrue(dialog.IsOpen, "the calendar opens by itself when a reward is waiting");
            Assert.IsTrue(daily.CanClaim);
            Assert.AreEqual(0, daily.TodayIndex);

            long coins = scene.Wallet.Coins;
            Assert.IsTrue(daily.Claim());
            Assert.AreEqual(coins + content.DailyRewards[0].coins, scene.Wallet.Coins);
            Assert.IsFalse(daily.CanClaim);
            Assert.IsFalse(daily.Claim(), "only once per day");

            _clock.Advance(DayMs);
            Assert.IsTrue(daily.CanClaim, "next day");
            Assert.AreEqual(1, daily.TodayIndex, "day 2 follows");
        }

        [UnityTest]
        public IEnumerator Streak_IsSaved_AndMaterialsGoToTheBarn()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            for (int day = 0; day < 3; day++)
            {
                Assert.IsTrue(scene.Daily.Claim(), $"day {day + 1}");
                _clock.Advance(DayMs);
            }

            var day3 = content.DailyRewards[2];
            Assert.IsNotNull(day3.material, "day 3 pays a building material");
            Assert.AreEqual(day3.materialAmount, scene.Inventory.Count(day3.material.Id));

            // Restart: the streak continues at day 4.
            scene.SaveNow();
            Object.DestroyImmediate(GameRoot.Current.gameObject);
            yield return null;
            SceneManager.LoadScene(SceneNames.MainMenu);
            float end = Time.realtimeSinceStartup + 5f;
            while (GameRoot.Current == null && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(GameRoot.Current.Context.ContinueGame().Succeeded);
            yield return LoadTown();
            scene = Object.FindAnyObjectByType<TownSceneController>();
            Assert.IsTrue(scene.Daily.CanClaim);
            Assert.AreEqual(3, scene.Daily.TodayIndex, "day 4 is next");

            // Skipping a day starts over.
            _clock.Advance(DayMs);
            Assert.AreEqual(0, scene.Daily.TodayIndex, "missed a day: back to day 1");
        }

        [UnityTest]
        public IEnumerator DevAllowClaimAgain_LetsTodayBeClaimedAgain()
        {
            yield return LoadTown();
            var daily = Object.FindAnyObjectByType<TownSceneController>().Daily;
            Assert.IsTrue(daily.Claim());
            Assert.IsFalse(daily.CanClaim);
            daily.DevAllowClaimAgain();
            Assert.IsTrue(daily.CanClaim);
            Assert.AreEqual(1, daily.TodayIndex, "the streak carries on");
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
