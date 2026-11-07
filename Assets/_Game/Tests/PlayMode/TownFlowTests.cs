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
    /// <summary>
    /// End-to-end checks of the real scenes (wiring, GameRoot, save/load). Saves go to a temp folder.
    /// Milestone 1 acceptance, minus the parts that need a real touchscreen.
    /// </summary>
    public sealed class TownFlowTests
    {
        private string _saveDir;

        [SetUp]
        public void SetUp()
        {
            DestroyRoot();
            _saveDir = Path.Combine(Path.GetTempPath(), "TownBuilderPlayModeTests", System.Guid.NewGuid().ToString("N"));
            GameRoot.SaveDirectoryOverride = _saveDir;
        }

        [TearDown]
        public void TearDown()
        {
            DestroyRoot();
            GameRoot.SaveDirectoryOverride = null;
            if (Directory.Exists(_saveDir)) Directory.Delete(_saveDir, true);
        }

        [UnityTest]
        public IEnumerator Boot_OpensMainMenu()
        {
            SceneManager.LoadScene(SceneNames.Boot);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == SceneNames.MainMenu, 5f);
            Assert.AreEqual(SceneNames.MainMenu, SceneManager.GetActiveScene().name);
            Assert.NotNull(GameRoot.Current, "GameRoot should survive the scene change");
        }

        [UnityTest]
        public IEnumerator NewTown_HasStarterTownHall_AndStartingCoins()
        {
            yield return LoadTown();
            var town = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;

            Assert.AreEqual(content.StarterBuildings.Count, town.Town.BuildingCount);
            Assert.IsTrue(town.Town.Buildings.Any(b => b.DefinitionId == "town_hall"));
            Assert.AreEqual(content.StartingCoins, town.Wallet.Coins);
            Assert.AreEqual(1, town.Progress.Level);
        }

        [UnityTest]
        public IEnumerator PlaceBuilding_ChargesCoins_AndSurvivesRestart()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            Assert.IsTrue(content.TryGetBuilding("cottage", out var cottage));
            long coinsBefore = scene.Wallet.Coins;

            Assert.IsTrue(scene.Placement.BeginPlacement(cottage));
            Assert.AreEqual(PlacementSystem.Mode.PlacingNew, scene.Placement.CurrentMode);
            Assert.AreEqual(PlacementResult.Valid, scene.Placement.PreviewValidity, "Preview should start on a free spot");
            Assert.IsTrue(scene.Placement.Confirm());

            Assert.AreEqual(PlacementSystem.Mode.Idle, scene.Placement.CurrentMode);
            Assert.AreEqual(coinsBefore - cottage.CoinCost, scene.Wallet.Coins);
            var placed = scene.Town.Buildings.Single(b => b.DefinitionId == "cottage");
            var placedOrigin = placed.Origin;
            int buildingCount = scene.Town.BuildingCount;

            scene.SaveNow();

            // Simulate closing and reopening the app: drop all in-memory state, then Continue.
            DestroyRoot();
            yield return null;
            SceneManager.LoadScene(SceneNames.MainMenu);
            yield return WaitFor(() => GameRoot.Current != null, 5f);
            Assert.NotNull(GameRoot.Current, "Main menu should recreate GameRoot");
            var load = GameRoot.Current.Context.ContinueGame();
            Assert.IsTrue(load.Succeeded, $"Load failed: {load.Status}");
            SceneManager.LoadScene(SceneNames.Town);
            yield return WaitForTownReady();

            var reloaded = Object.FindAnyObjectByType<TownSceneController>();
            Assert.AreEqual(buildingCount, reloaded.Town.BuildingCount);
            Assert.AreEqual(coinsBefore - cottage.CoinCost, reloaded.Wallet.Coins);
            Assert.AreEqual(placedOrigin, reloaded.Town.Buildings.Single(b => b.DefinitionId == "cottage").Origin);
        }

        [UnityTest]
        public IEnumerator MoveAndRemove_UpdateTownState()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            GameRoot.Current.Context.Content.TryGetBuilding("barn", out var barn);

            scene.Placement.BeginPlacement(barn);
            scene.Placement.Confirm();
            var placed = scene.Town.Buildings.Single(b => b.DefinitionId == "barn");
            var start = placed.Origin;

            // Select by id (tapping with a real finger is covered by the device checklist).
            var target = new GridCoord(start.X - 3, start.Y);
            scene.Placement.SelectBuilding(placed.InstanceId);
            Assert.AreEqual(PlacementSystem.Mode.Selected, scene.Placement.CurrentMode);

            Assert.IsTrue(scene.Placement.BeginMoveSelected());
            scene.Placement.SetPreviewOrigin(target);
            if (scene.Placement.PreviewValidity == PlacementResult.Valid)
            {
                Assert.IsTrue(scene.Placement.Confirm());
                Assert.AreEqual(target, placed.Origin);
            }
            else
            {
                scene.Placement.Cancel();
                Assert.AreEqual(start, placed.Origin, "Cancel must restore the original position");
            }

            Assert.AreEqual(PlacementSystem.Mode.Selected, scene.Placement.CurrentMode);
            Assert.IsTrue(scene.Placement.RemoveSelected());
            Assert.IsFalse(scene.Town.Buildings.Any(b => b.DefinitionId == "barn"));
        }

        [UnityTest]
        public IEnumerator TownHall_CannotBeRemovedOrBuiltTwice()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var hall = scene.Town.Buildings.Single(b => b.DefinitionId == "town_hall");
            scene.Placement.SelectBuilding(hall.InstanceId);

            Assert.IsFalse(scene.Placement.RemoveSelected());
            Assert.IsTrue(scene.Town.TryGet(hall.InstanceId, out _));

            GameRoot.Current.Context.Content.TryGetBuilding("town_hall", out var hallDefinition);
            Assert.IsFalse(scene.Placement.CanBuildMore(hallDefinition), "Town Hall is limited to one");
            Assert.IsFalse(scene.Placement.BeginPlacement(hallDefinition));
        }

        [UnityTest]
        public IEnumerator RewardedAdButton_GrantsCoins_AndSaves()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var config = GameRoot.Current.Context.Content.AdsConfig;
            Assert.NotNull(config, "GameContent should reference AdsConfig");
            Assert.IsInstanceOf<SimulatedAdService>(GameRoot.Current.Context.Ads, "Editor must never call real ads");

            var adButton = Object.FindAnyObjectByType<RewardedCoinsButton>();
            Assert.NotNull(adButton, "Rewarded ad button missing from the HUD");
            var button = adButton.GetComponent<UnityEngine.UI.Button>();
            Assert.IsTrue(button.interactable);

            long before = scene.Wallet.Coins;
            button.onClick.Invoke();
            Assert.AreEqual(before + config.RewardedCoins, scene.Wallet.Coins);

            scene.SaveNow();
            Assert.AreEqual(before + config.RewardedCoins, GameRoot.Current.Context.SaveStore.Load().Data.player.coins);
        }

        // ------------------------------------------------------------------ helpers

        private static IEnumerator LoadTown()
        {
            SceneManager.LoadScene(SceneNames.Town);
            yield return WaitForTownReady();
        }

        private static IEnumerator WaitForTownReady()
        {
            yield return WaitFor(() =>
            {
                var c = Object.FindAnyObjectByType<TownSceneController>();
                return c != null && c.IsReady;
            }, 5f);
            var controller = Object.FindAnyObjectByType<TownSceneController>();
            Assert.NotNull(controller);
            Assert.IsTrue(controller.IsReady, "Town scene did not initialise");
        }

        private static IEnumerator WaitFor(System.Func<bool> condition, float timeoutSeconds)
        {
            float end = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        }

        private static void DestroyRoot()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
        }
    }
}
