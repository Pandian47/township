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
    /// <summary>Order board and level-ups in the real Town scene.</summary>
    public sealed class OrdersFlowTests
    {
        private string _saveDir;
        private ManualClock _clock;

        [SetUp]
        public void SetUp()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            _saveDir = Path.Combine(Path.GetTempPath(), "TownBuilderOrderTests", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator NewTown_HasFullOrderBoard_WithOnlyUnlockedItems()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var board = scene.Orders.Board;
            var content = GameRoot.Current.Context.Content;

            for (int i = 0; i < board.SlotCount; i++)
            {
                var order = board.Get(i);
                Assert.NotNull(order, $"slot {i} should have an order");
                foreach (var line in order.Lines)
                {
                    // Level 1: only wheat and bread are unlocked (corn and corn bread unlock at level 2).
                    StringAssert.DoesNotContain("corn", line.ItemId);
                    Assert.IsTrue(content.TryGetItem(line.ItemId, out _));
                }
            }
        }

        [UnityTest]
        public IEnumerator DeliverOrder_PaysAndRefills_AndOrdersPersist()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var order = scene.Orders.Board.Get(0);
            foreach (var line in order.Lines) scene.Inventory.TryAdd(line.ItemId, line.Amount);

            long coins = scene.Wallet.Coins;
            long xp = scene.Progress.Xp;
            Assert.IsTrue(scene.Orders.CanDeliver(0));
            Assert.AreEqual(DeliverResult.Delivered, scene.Orders.Deliver(0));
            Assert.GreaterOrEqual(scene.Wallet.Coins, coins + order.RewardCoins);
            Assert.AreEqual(xp + order.RewardXp, scene.Progress.Xp);
            Assert.NotNull(scene.Orders.Board.Get(0), "a new order replaces the delivered one");

            // Skip slot 1, then restart: the skip cooldown and orders survive.
            scene.Orders.Skip(1);
            var skipped = scene.Orders.Board.Get(1);
            Assert.IsFalse(skipped.IsAvailable(_clock.UtcNowUnixMs));
            var slot2 = scene.Orders.Board.Get(2);

            scene.SaveNow();
            Object.DestroyImmediate(GameRoot.Current.gameObject);
            yield return null;
            SceneManager.LoadScene(SceneNames.MainMenu);
            yield return WaitFor(() => GameRoot.Current != null);
            Assert.IsTrue(GameRoot.Current.Context.ContinueGame().Succeeded);
            SceneManager.LoadScene(SceneNames.Town);
            yield return WaitForTown();
            scene = Object.FindAnyObjectByType<TownSceneController>();

            Assert.AreEqual(skipped.AvailableAtUnixMs, scene.Orders.Board.Get(1).AvailableAtUnixMs);
            Assert.AreEqual(slot2.RewardCoins, scene.Orders.Board.Get(2).RewardCoins);
            _clock.Advance(GameRoot.Current.Context.Content.OrderSkipCooldownMs);
            Assert.IsTrue(scene.Orders.Board.Get(1).IsAvailable(_clock.UtcNowUnixMs));
        }

        [UnityTest]
        public IEnumerator LevelUp_PaysReward_ShowsDialog_UnlocksCorn()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            content.TryGetCrop("corn", out var corn);
            Assert.IsFalse(scene.Farming.IsUnlocked(corn), "corn is locked at level 1");

            LevelUpInfo? info = null;
            scene.Progression.LeveledUp += i => info = i;
            long coins = scene.Wallet.Coins;
            scene.Progress.AddXp(content.CreateLevelCurve().XpToNext(1));

            Assert.AreEqual(2, scene.Progress.Level);
            Assert.NotNull(info);
            Assert.AreEqual(coins + content.LevelUpCoinsPerLevel * 2, scene.Wallet.Coins);
            CollectionAssert.Contains(info.Value.Unlocks, "Corn");
            Assert.IsTrue(scene.Farming.IsUnlocked(corn));
            Assert.IsTrue(Object.FindAnyObjectByType<LevelUpDialog>().IsOpen, "level-up dialog is shown");
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
