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
    /// <summary>Milestone 8 step 1: the delivery truck end to end.</summary>
    public sealed class TruckFlowTests
    {
        private string _saveDir;
        private ManualClock _clock;

        [SetUp]
        public void SetUp()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            _saveDir = Path.Combine(Path.GetTempPath(), "TownBuilderTruckTests", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator Depot_Truck_LoadSendReturn_PaysOnReturn_AndIsSaved()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            content.TryGetBuilding(content.TruckDepotId, out var depotDef);
            Assert.IsNotNull(depotDef);
            Assert.AreEqual(11, depotDef.UnlockLevel);
            Assert.IsFalse(scene.Truck.HasDepot);
            Assert.IsFalse(scene.Placement.BeginPlacement(depotDef), "depot needs level 11");

            DevTools.UnlockAll(scene);
            Assert.GreaterOrEqual(scene.Progress.Level, 11, "DEV unlock-all reaches the depot");
            scene.Wallet.AddCoins(10_000);
            Assert.IsTrue(scene.Placement.BeginPlacement(depotDef));
            Assert.IsTrue(scene.Placement.Confirm());
            string depot = scene.Town.Buildings.First(b => b.DefinitionId == depotDef.Id).InstanceId;
            Assert.IsFalse(scene.Truck.HasDepot, "under construction: no truck yet");
            scene.Construction.FinishNow(depot);
            yield return new WaitForSeconds(0.6f); // controller tick

            var truck = scene.Truck.Truck;
            Assert.IsTrue(truck.IsAtDepot, "the truck parks at the new depot");
            Assert.AreEqual(content.TruckLoads, truck.Loads.Count);
            Assert.IsTrue(truck.Loads.All(l => l.Lines.Count == content.TruckLinesPerLoad));
            Assert.IsTrue(scene.Truck.BadgeVisible, "truck badge above the depot");
            Assert.IsFalse(scene.Truck.SendOff(), "can't leave empty");

            // Load the first load.
            var load = truck.Loads[0];
            foreach (var line in load.Lines) scene.Inventory.TryAdd(line.ItemId, line.Amount);
            Assert.AreEqual(TruckLoadResult.Loaded, scene.Truck.Load(0));

            long coins = scene.Wallet.Coins;
            Assert.IsTrue(scene.Truck.SendOff());
            Assert.AreEqual(coins, scene.Wallet.Coins, "nothing paid when it leaves");
            Assert.AreEqual(TruckState.Away, truck.State);
            Assert.IsFalse(scene.Truck.BadgeVisible);

            // Restart halfway: still away, cargo kept.
            _clock.Advance(content.TruckTripSeconds * 500L);
            scene.SaveNow();
            Object.DestroyImmediate(GameRoot.Current.gameObject);
            yield return null;
            SceneManager.LoadScene(SceneNames.MainMenu);
            float end = Time.realtimeSinceStartup + 5f;
            while (GameRoot.Current == null && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(GameRoot.Current.Context.ContinueGame().Succeeded);
            yield return LoadTown();
            scene = Object.FindAnyObjectByType<TownSceneController>();
            truck = scene.Truck.Truck;
            Assert.AreEqual(TruckState.Away, truck.State);
            Assert.AreEqual(load.RewardCoins, truck.Cargo.Coins);

            // Call back with gems: pays the cargo.
            int cost = scene.Truck.GemCostToCallBack;
            Assert.AreEqual(TimeSkip.GemCost(content.TruckTripSeconds * 500L), cost);
            scene.Wallet.AddGems(100);
            long gems = scene.Wallet.Gems;
            coins = scene.Wallet.Coins;
            Assert.IsTrue(scene.Truck.CallBackWithGems());
            Assert.AreEqual(gems - cost, scene.Wallet.Gems);
            Assert.GreaterOrEqual(scene.Wallet.Coins, coins + load.RewardCoins, "earnings paid on return");
            Assert.IsTrue(truck.IsAtDepot, "back with new loads");
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
