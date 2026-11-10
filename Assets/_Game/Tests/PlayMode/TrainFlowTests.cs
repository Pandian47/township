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

            // Every town has the station at its fixed spot by the railway, closed until its level.
            string station = scene.Town.Buildings.Single(b => b.DefinitionId == stationDef.Id).InstanceId;
            Assert.AreEqual(content.StarterBuildings.First(s => s.building == stationDef).origin.x, scene.Town.Buildings.Single(b => b.InstanceId == station).Origin.X);
            Assert.IsTrue(train.IsStation(station));
            Assert.IsFalse(train.IsOpen);
            Assert.IsFalse(train.HasStation);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.IsTrue(train.LockBadgeVisible, "lock badge while closed");
            Assert.IsFalse(train.Train.IsAtStation, "no train before the station opens");

            // Not for sale, not movable, not removable.
            scene.Placement.SelectBuilding(station);
            Assert.IsFalse(scene.Placement.BeginMoveSelected(), "the station can't be moved");
            Assert.IsFalse(scene.Placement.RemoveSelected(), "or removed");
            scene.Placement.Cancel();
            Assert.IsFalse(scene.Placement.BeginPlacement(stationDef), "and there is no second one to buy");

            // Reaching its level opens it; the first train rolls in along the railway.
            var mover = Object.FindAnyObjectByType<TrainMover>();
            Assert.NotNull(mover, "train mover in the scene");
            mover.TravelSeconds = 0.6f;
            var view = Object.FindObjectsByType<BuildingView>().First(v => v.InstanceId == station);

            scene.Progress.AddXp(10_000);
            Assert.GreaterOrEqual(scene.Progress.Level, train.UnlockLevel);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.IsTrue(train.IsOpen);
            Assert.IsFalse(train.LockBadgeVisible);
            Assert.IsTrue(train.Train.IsAtStation, "the first train arrives when the station opens");
            Assert.AreEqual(content.TrainCrates, train.Train.Crates.Count);
            Assert.IsTrue(mover.IsArriving, "it rolls in along the railway");
            Assert.AreEqual(stationDef.Sprite, view.SpriteRenderer.sprite, "empty platform until the train has stopped");

            yield return new WaitForSecondsRealtime(0.8f);
            Assert.IsFalse(mover.IsMoving, "stopped");
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
            Assert.IsTrue(mover.IsDeparting, "the train pulls away along the railway");
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.Greater(mover.CurrentY, 17f, "moving towards the far end of the map");
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.IsFalse(mover.IsMoving, "gone");
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
            scene.Progress.AddXp(10_000);
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

        [UnityTest]
        public IEnumerator OldSave_GetsTheStation_AndABuildingInTheWayMoves()
        {
            // A save from before the fixed station: a cottage right where the station goes, no station.
            var save = NewGameFactory.Create(0, 1500, 20);
            save.buildings.Add(new BuildingSaveData { instanceId = "cottage_on_spot", definitionId = "cottage", x = 10, y = 19 });
            new JsonSaveStore(_saveDir).Save(save);

            SceneManager.LoadScene(SceneNames.MainMenu);
            float end = Time.realtimeSinceStartup + 5f;
            while (GameRoot.Current == null && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(GameRoot.Current.Context.ContinueGame().Succeeded);
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;

            var station = scene.Town.Buildings.Single(b => b.DefinitionId == content.TrainStationId);
            var spot = content.StarterBuildings.First(s => s.building != null && s.building.Id == content.TrainStationId).origin;
            Assert.AreEqual(new GridCoord(spot.x, spot.y), station.Origin, "station at its fixed spot");
            Assert.IsTrue(scene.Town.TryGet("cottage_on_spot", out var cottage), "the cottage is kept");
            Assert.AreNotEqual(new GridCoord(10, 19), cottage.Origin, "but moved out of the way");
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
