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
    /// <summary>Milestone 7 step 4: weekend events end to end (the clock is set so "today" is chosen by the test).</summary>
    public sealed class WeekendEventFlowTests
    {
        private const long DayMs = 86_400_000L;
        private const long Wednesday = 20768; // 2026-11-11
        private string _saveDir;
        private ManualClock _clock;

        [SetUp]
        public void SetUp()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            _saveDir = Path.Combine(Path.GetTempPath(), "TownBuilderEventTests", System.Guid.NewGuid().ToString("N"));
            GameRoot.SaveDirectoryOverride = _saveDir;
            // Noon local time on a Wednesday (the offset is the machine's time zone).
            long offset = (long)System.TimeZoneInfo.Local.GetUtcOffset(System.DateTime.UtcNow).TotalMilliseconds;
            _clock = new ManualClock(Wednesday * DayMs + DayMs / 2 - offset);
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
        public IEnumerator Weekday_NoEvent_Saturday_EventStarts_Monday_Ends()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var panel = Object.FindAnyObjectByType<EventPanel>();
            Assert.AreEqual(2, GameRoot.Current.Context.Content.WeekendEvents.Count);
            Assert.IsFalse(scene.Events.IsActive, "Wednesday: no event");
            Assert.IsFalse(panel.ButtonVisible);
            Assert.AreEqual(3, scene.Events.DaysUntilNext);

            _clock.Advance(3 * DayMs); // Saturday noon
            scene.Events.Tick();
            Assert.IsTrue(scene.Events.IsActive);
            Assert.IsTrue(panel.ButtonVisible, "event button shows");
            Assert.AreEqual(1.5 * DayMs, scene.Events.RemainingMs, 1000, "ends Monday 00:00");

            _clock.Advance(2 * DayMs); // Monday noon
            scene.Events.Tick();
            Assert.IsFalse(scene.Events.IsActive);
            Assert.IsFalse(panel.ButtonVisible);
        }

        [UnityTest]
        public IEnumerator Points_Prizes_AndDecorationUnlock_AreSaved()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            var shop = Object.FindAnyObjectByType<BuildingShopPanel>();
            scene.Events.DevStartNow();
            Assert.IsTrue(scene.Events.IsActive, "DEV starts an event on a weekday");
            var ev = scene.Events.Current;

            // Earn points the way this event counts them.
            var last = ev.Milestones.Last();
            if (ev.PointsFrom == AchievementKind.DeliverOrders)
                for (int i = 0; i < last.goal; i++) scene.Events.Progress.AddPoints(1);
            else
                scene.Events.Progress.AddPoints(last.goal);

            long coins = scene.Wallet.Coins, gems = scene.Wallet.Gems;
            for (int i = 0; i < ev.Milestones.Count; i++) Assert.IsTrue(scene.Events.Claim(i), $"prize {i + 1}");
            Assert.AreEqual(coins + ev.Milestones.Sum(m => m.coins), scene.Wallet.Coins);
            Assert.AreEqual(gems + ev.Milestones.Sum(m => m.gems), scene.Wallet.Gems);
            Assert.IsFalse(scene.Events.Claim(0), "each prize once");

            var deco = last.decoration;
            Assert.IsNotNull(deco);
            Assert.IsTrue(deco.EventOnly);
            Assert.IsTrue(scene.Events.IsListedInShop(deco), "prize unlocked the decoration in the shop");
            var other = content.Buildings.First(b => b.EventOnly && b != deco);
            Assert.IsFalse(scene.Events.IsListedInShop(other), "the other event's decoration is still hidden");

            shop.Open();
            shop.OpenCategory(BuildingCategory.Decoration);
            int listed = content.Buildings.Count(b => b.ShowInShop && b.Category == BuildingCategory.Decoration && (!b.EventOnly || b == deco)
                                                      && (!b.IsSeasonal || b.Season == scene.Seasons.Current));
            Assert.AreEqual(listed, shop.VisibleItems);
            shop.Close();

            // Restart: prizes and unlock kept.
            scene.SaveNow();
            Object.DestroyImmediate(GameRoot.Current.gameObject);
            yield return null;
            SceneManager.LoadScene(SceneNames.MainMenu);
            float end = Time.realtimeSinceStartup + 5f;
            while (GameRoot.Current == null && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(GameRoot.Current.Context.ContinueGame().Succeeded);
            yield return LoadTown();
            scene = Object.FindAnyObjectByType<TownSceneController>();
            Assert.IsTrue(scene.Events.IsListedInShop(deco), "unlock is kept forever");
            Assert.AreEqual(ev.Milestones.Count, scene.Events.Progress.Claimed);
        }

        [UnityTest]
        public IEnumerator HarvestingCounts_ForTheHarvestFestival()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            scene.Events.DevStartNow();
            if (scene.Events.Current.PointsFrom != AchievementKind.HarvestCrops) _clock.Advance(7 * DayMs); // other week
            scene.Events.DevStartNow();
            Assert.AreEqual(AchievementKind.HarvestCrops, scene.Events.Current.PointsFrom);

            content.TryGetCrop("wheat", out var wheat);
            var fieldId = scene.Town.Buildings.First(b => b.DefinitionId == "field").InstanceId;
            int before = scene.Events.Progress.Points;
            Assert.AreEqual(PlantResult.Planted, scene.Farming.Plant(fieldId, wheat));
            _clock.Advance(wheat.GrowDurationMs);
            Assert.IsTrue(scene.Farming.TryHarvest(fieldId));
            Assert.AreEqual(before + wheat.HarvestYield, scene.Events.Progress.Points);
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
