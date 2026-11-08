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
    /// <summary>Visual feedback: reward labels, building pops, selection outline, top-bar pops.</summary>
    public sealed class EffectsFlowTests
    {
        private string _saveDir;
        private ManualClock _clock;

        [SetUp]
        public void SetUp()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            _saveDir = Path.Combine(Path.GetTempPath(), "TownBuilderEffectsTests", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator Harvest_ShowsFloatingReward_AndPopsField()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            content.TryGetCrop("wheat", out var wheat);
            string field = scene.Town.Buildings.First(b => b.DefinitionId == "field").InstanceId;

            WorldReward reward = null;
            scene.Farming.Rewarded += r => reward = r;
            scene.Farming.Plant(field, wheat);
            scene.Farming.FinishNow(field);
            Assert.IsTrue(scene.Farming.TryHarvest(field));

            Assert.NotNull(reward);
            Assert.AreEqual(field, reward.BuildingId);
            Assert.AreEqual(wheat, reward.Items[0].Item);
            Assert.AreEqual(wheat.HarvestYield, reward.Items[0].Amount);
            Assert.AreEqual(wheat.HarvestXp, reward.Xp);

            var labels = scene.Effects.FloatingText.ActiveTexts().ToList();
            CollectionAssert.Contains(labels, $"+{wheat.HarvestYield} {wheat.DisplayName}");
            CollectionAssert.Contains(labels, $"+{wheat.HarvestXp} XP");

            var view = FindView(field);
            var pop = view.GetComponent<PopAnimator>();
            Assert.NotNull(pop);
            Assert.IsTrue(pop.IsPlaying);

            // Both effects end on their own and the field returns to its normal size.
            yield return new WaitForSecondsRealtime(1.7f);
            Assert.AreEqual(0, scene.Effects.FloatingText.ActiveCount);
            Assert.IsFalse(pop.IsPlaying);
            Assert.AreEqual(Vector3.one, view.transform.localScale);
        }

        [UnityTest]
        public IEnumerator CollectAndConstruction_ShowRewards()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            content.TryGetBuilding("bakery", out var bakeryDef);
            content.TryGetRecipe("bake_bread", out var bread);

            Assert.IsTrue(scene.Placement.BeginPlacement(bakeryDef));
            Assert.IsTrue(scene.Placement.Confirm());
            string bakeryId = scene.Town.Buildings.First(b => b.DefinitionId == "bakery").InstanceId;
            Assert.IsTrue(FindView(bakeryId).GetComponent<PopAnimator>().IsPlaying, "placing pops the building");

            Assert.IsTrue(scene.Construction.FinishNow(bakeryId));
            if (bakeryDef.BuildXp > 0)
                CollectionAssert.Contains(scene.Effects.FloatingText.ActiveTexts().ToList(), $"+{bakeryDef.BuildXp} XP");

            scene.Inventory.TryAdd("wheat", 4);
            scene.Production.Enqueue(bakeryId, bread);
            scene.Production.Enqueue(bakeryId, bread);
            _clock.Advance(bread.DurationMs * 2);
            Assert.AreEqual(2, scene.Production.Collect(bakeryId));
            CollectionAssert.Contains(scene.Effects.FloatingText.ActiveTexts().ToList(), $"+{bread.OutputAmount * 2} {bread.Output.DisplayName}",
                "two loaves of the same product are merged into one line");
        }

        [UnityTest]
        public IEnumerator Selecting_ShowsOutline_Deselecting_HidesIt()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var outline = scene.Effects.Outline;
            string hall = scene.Town.Buildings.First(b => b.DefinitionId == "town_hall").InstanceId;
            Assert.IsFalse(outline.IsShowing);

            scene.Placement.SelectBuilding(hall);
            Assert.IsTrue(outline.IsShowing);
            Assert.AreSame(FindView(hall), outline.Target);
            Assert.Greater(outline.RimRendererCount, 0);

            Assert.IsTrue(scene.Placement.BeginMoveSelected());
            Assert.IsFalse(outline.IsShowing, "no outline while moving (the preview tint shows validity)");

            scene.Placement.Cancel(); // back to Selected
            Assert.IsTrue(outline.IsShowing);

            scene.Placement.Cancel(); // Idle
            Assert.IsFalse(outline.IsShowing);
            yield return null;
            Assert.AreEqual(0, FindView(hall).GetComponentsInChildren<SpriteMask>().Length, "outline parts are cleaned up");
        }

        [UnityTest]
        public IEnumerator RemovingSelectedBuilding_ClearsOutline()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            GameRoot.Current.Context.Content.TryGetBuilding("cottage", out var cottage);
            Assert.IsTrue(scene.Placement.BeginPlacement(cottage));
            Assert.IsTrue(scene.Placement.Confirm());
            string id = scene.Town.Buildings.First(b => b.DefinitionId == "cottage").InstanceId;

            scene.Placement.SelectBuilding(id);
            Assert.IsTrue(scene.Effects.Outline.IsShowing);
            Assert.IsTrue(scene.Placement.RemoveSelected());
            yield return null;
            Assert.IsFalse(scene.Effects.Outline.IsShowing);
        }

        [UnityTest]
        public IEnumerator TopBarCoins_PopWhenCoinsGoUp()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var coinsPill = GameObject.Find("CoinsPill");
            var before = coinsPill.GetComponent<PopAnimator>();
            Assert.IsTrue(before == null || !before.IsPlaying, "no pop on first display");

            scene.Wallet.AddCoins(10);
            var pop = coinsPill.GetComponent<PopAnimator>();
            Assert.NotNull(pop);
            Assert.IsTrue(pop.IsPlaying);
        }

        private static BuildingView FindView(string instanceId) =>
            Object.FindObjectsByType<BuildingView>().First(v => v.InstanceId == instanceId);

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
