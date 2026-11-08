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
    /// <summary>Milestone 5 step 2: Train Station, loading crates, sending the train, its return and persistence.</summary>
    public sealed class TrainFlowTests
    {
        private string _saveDir;
        private ManualClock _clock;

        [SetUp]
        public void SetUp()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            _saveDir = Path.Combine(Path.GetTempPath(), "TownBuilderTrainTests", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator Station_Train_LoadSendReturn_AndRestart()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            var train = scene.Train;
            content.TryGetBuilding(content.TrainStationId, out var stationDef);

            Assert.IsFalse(train.HasStation);
            Assert.IsFalse(scene.Placement.BeginPlacement(stationDef), "station needs level 6");

            scene.Progress.AddXp(10_000);
            scene.Wallet.AddCoins(10_000);
            Assert.IsTrue(scene.Placement.BeginPlacement(stationDef));
            Assert.IsTrue(scene.Placement.Confirm());
            string station = scene.Town.Buildings.First(b => b.DefinitionId == stationDef.Id).InstanceId;
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.IsFalse(train.Train.IsAtStation, "no train while the station is being built");

            Assert.IsTrue(scene.Construction.FinishNow(station));
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.IsTrue(train.IsStation(station));
            Assert.IsTrue(train.Train.IsAtStation, "the first train arrives when the station is finished");
            Assert.AreEqual(content.TrainCrates, train.Train.Crates.Count);

            var view = Object.FindObjectsByType<BuildingView>().First(v => v.InstanceId == station);
            Assert.AreEqual(content.TrainAtStationSprite, view.SpriteRenderer.sprite, "station shows the train");

            // Load every crate, then send the full train for the bonus.
            foreach (var crate in train.Train.Crates) scene.Inventory.TryAdd(crate.ItemId, crate.Amount);
            long coins = scene.Wallet.Coins;
            long crateCoins = train.Train.Crates.Sum(c => c.RewardCoins);
            for (int i = 0; i < train.Train.Crates.Count; i++)
                Assert.AreEqual(CrateFillResult.Filled, train.Fill(i));
            Assert.IsTrue(train.Train.AllFilled);
            Assert.AreEqual(coins + crateCoins, scene.Wallet.Coins);

            long bonus = train.FullTrainBonus;
            Assert.Greater(bonus, 0);
            Assert.IsTrue(train.SendOff());
            Assert.AreEqual(coins + crateCoins + bonus, scene.Wallet.Coins);
            Assert.IsFalse(train.Train.IsAtStation);
            Assert.AreEqual(stationDef.Sprite, view.SpriteRenderer.sprite, "empty platform while the train is away");
            Assert.AreEqual(content.TrainTravelSeconds * 1000L, train.RemainingMs);

            // Close the app while it travels; it is back on reopening.
            scene.SaveNow();
            Object.DestroyImmediate(GameRoot.Current.gameObject);
            _clock.Advance(content.TrainTravelSeconds * 1000L);
            yield return null;
            SceneManager.LoadScene(SceneNames.MainMenu);
            float end = Time.realtimeSinceStartup + 5f;
            while (GameRoot.Current == null && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(GameRoot.Current.Context.ContinueGame().Succeeded);
            yield return LoadTown();
            scene = Object.FindAnyObjectByType<TownSceneController>();

            Assert.IsTrue(scene.Train.Train.IsAtStation, "train came back while the app was closed");
            Assert.AreEqual(0, scene.Train.Train.FilledCount, "with fresh crates");
        }

        [UnityTest]
        public IEnumerator PartlyLoadedTrain_IsKeptAcrossRestart_AndPaysNoBonus()
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

            var first = scene.Train.Train.Crates[0];
            scene.Inventory.TryAdd(first.ItemId, first.Amount);
            Assert.AreEqual(CrateFillResult.Filled, scene.Train.Fill(0));
            var crateIds = scene.Train.Train.Crates.Select(c => c.ItemId).ToList();

            scene.SaveNow();
            Object.DestroyImmediate(GameRoot.Current.gameObject);
            yield return null;
            SceneManager.LoadScene(SceneNames.MainMenu);
            float end = Time.realtimeSinceStartup + 5f;
            while (GameRoot.Current == null && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(GameRoot.Current.Context.ContinueGame().Succeeded);
            yield return LoadTown();
            scene = Object.FindAnyObjectByType<TownSceneController>();

            CollectionAssert.AreEqual(crateIds, scene.Train.Train.Crates.Select(c => c.ItemId).ToList(), "same crates");
            Assert.AreEqual(1, scene.Train.Train.FilledCount);
            long coins = scene.Wallet.Coins;
            Assert.IsTrue(scene.Train.SendOff());
            Assert.AreEqual(coins, scene.Wallet.Coins, "no bonus for a partly loaded train");
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
