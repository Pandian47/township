using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TownBuilder.Domain;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TownBuilder.Tests
{
    /// <summary>
    /// v0.41.0 production window: opens over the darkened town, products fly into baskets, ‹ › step between factories,
    /// the padlock basket buys a slot, the Collect tray collects, Close deselects, and Reduce motion makes it instant.
    /// </summary>
    public sealed class FactoryWindowFlowTests
    {
        private string _saveDir;
        private ManualClock _clock;
        private bool _reduceMotionBefore;

        [SetUp]
        public void SetUp()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            _saveDir = Path.Combine(Path.GetTempPath(), "TownBuilderFactoryWindowTests", System.Guid.NewGuid().ToString("N"));
            _clock = new ManualClock(1_700_000_000_000);
            GameRoot.SaveDirectoryOverride = _saveDir;
            GameRoot.ClockOverride = _clock;
            _reduceMotionBefore = MotionPreferences.ReduceMotion;
            MotionPreferences.ReduceMotion = false;
        }

        [TearDown]
        public void TearDown()
        {
            MotionPreferences.ReduceMotion = _reduceMotionBefore;
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            GameRoot.SaveDirectoryOverride = null;
            GameRoot.ClockOverride = null;
            if (Directory.Exists(_saveDir)) Directory.Delete(_saveDir, true);
        }

        [UnityTest]
        public IEnumerator Window_Queue_Navigate_BuySlot_Collect_Close()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            content.TryGetBuilding("bakery", out var bakeryDef);
            content.TryGetRecipe("bake_bread", out var bread);
            scene.Wallet.AddCoins(5000);
            string first = BuildBakery(scene, bakeryDef);
            BuildBakery(scene, bakeryDef);
            scene.Inventory.TryAdd("wheat", 10);
            var factories = scene.Production.BuiltFactoryIds().ToList();
            string second = factories[(factories.IndexOf(first) + 1) % factories.Count];

            var panel = Object.FindAnyObjectByType<FactoryPanel>(FindObjectsInactive.Include);
            var window = panel.transform.Find("Root/Window");
            var inner = window.Find("Inner");

            // Selecting the bakery opens the window; it grows in from 92%.
            scene.Placement.SelectBuilding(first);
            yield return null;
            Assert.IsTrue(panel.IsOpen);
            Assert.AreEqual(first, panel.FactoryId);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(1f, window.localScale.x, 0.001f, "fully open");
            Assert.AreEqual("BAKERY", window.Find("Sign/Title/Text").GetComponent<TMPro.TMP_Text>().text);

            // Tapping a product flies its icon into the first basket.
            var breadBadge = panel.GetComponentsInChildren<RecipeButtonView>().First(b => b.Recipe == bread);
            breadBadge.GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(1, scene.Production.GetQueue(first).Jobs.Count);
            Assert.AreEqual(1, panel.FlyingIcons, "the icon is on its way");
            yield return WaitFor(() => panel.FlyingIcons == 0);
            var slots = panel.GetComponentsInChildren<QueueSlotView>();
            Assert.AreEqual(QueueSlotView.State.Active, slots[0].Current);
            Assert.IsTrue(slots[0].Icon.enabled, "the icon shows in the basket once it lands");

            // The machine: lamp dark while nothing is ready; the belt runs while something is being made.
            var lamp = inner.Find("CollectTray/Lamp/Light").GetComponent<Image>();
            Assert.Less(lamp.color.g, 0.5f, "lamp off: nothing ready");
            var belt = panel.GetComponentInChildren<BeltScroller>(true);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.Greater(belt.Offset, 0f, "the belt is running");

            // › steps to the other bakery (the town selects it), ‹ comes back.
            Assert.IsTrue(window.Find("NextButton").gameObject.activeSelf, "two factories: arrows shown");
            window.Find("NextButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.AreEqual(second, scene.Placement.TargetInstanceId);
            Assert.AreEqual(second, panel.FactoryId);
            window.Find("PreviousButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.AreEqual(first, panel.FactoryId);

            // The padlock basket buys a slot (after confirming).
            int before = scene.Production.GetQueue(first).Slots;
            inner.Find("Slots/AddSlotButton").GetComponent<Button>().onClick.Invoke();
            var confirm = Object.FindAnyObjectByType<ConfirmDialog>(FindObjectsInactive.Include).GetComponentsInChildren<Button>(true)
                .First(b => b.name == "ConfirmButton");
            confirm.onClick.Invoke();
            Assert.AreEqual(before + 1, scene.Production.GetQueue(first).Slots);

            // When the bread is ready, the tray collects it.
            _clock.Advance(bread.DurationMs + 1000);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreEqual(QueueSlotView.State.Ready, slots[0].Current);
            Assert.Greater(lamp.color.g, 0.9f, "lamp on: a product is ready");
            var collect = inner.Find("CollectTray/CollectButton").GetComponent<Button>();
            Assert.IsTrue(collect.interactable);
            int breadBefore = scene.Inventory.Count("bread");
            collect.onClick.Invoke();
            Assert.Greater(scene.Inventory.Count("bread"), breadBefore, "collected");
            Assert.IsFalse(collect.interactable, "nothing left to collect");

            // Close deselects the factory; the window shrinks away.
            window.Find("CloseButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.IsNull(panel.FactoryId);
            Assert.AreEqual(PlacementSystem.Mode.Idle, scene.Placement.CurrentMode);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.IsFalse(panel.IsOpen);
        }

        [UnityTest]
        public IEnumerator ReduceMotion_OpensAndClosesAtOnce_NoFlyingIcons()
        {
            MotionPreferences.ReduceMotion = true;
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            content.TryGetBuilding("bakery", out var bakeryDef);
            content.TryGetRecipe("bake_bread", out var bread);
            scene.Wallet.AddCoins(5000);
            string id = BuildBakery(scene, bakeryDef);
            scene.Inventory.TryAdd("wheat", 4);

            var panel = Object.FindAnyObjectByType<FactoryPanel>(FindObjectsInactive.Include);
            var window = panel.transform.Find("Root/Window");
            scene.Placement.SelectBuilding(id);
            yield return null;
            Assert.AreEqual(1f, window.localScale.x, 0.001f, "no grow-in");
            Assert.AreEqual(1f, panel.transform.Find("Root").GetComponent<CanvasGroup>().alpha, 0.001f, "no fade");
            Assert.AreEqual(scene.Production.BuiltFactoryIds().Count() > 1, window.Find("PreviousButton").gameObject.activeSelf,
                "arrows only with more than one factory");

            panel.GetComponentsInChildren<RecipeButtonView>().First(b => b.Recipe == bread).GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(0, panel.FlyingIcons);
            Assert.AreEqual(QueueSlotView.State.Active, panel.GetComponentsInChildren<QueueSlotView>()[0].Current);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreEqual(0f, panel.GetComponentInChildren<BeltScroller>(true).Offset, "the belt stands still with Reduce motion");

            scene.Placement.Cancel();
            yield return null;
            Assert.IsFalse(panel.IsOpen, "closed at once");
        }

        [UnityTest]
        public IEnumerator Settings_ReduceMotionButton_Toggles()
        {
            yield return LoadTown();
            var settings = Object.FindAnyObjectByType<SettingsPanel>(FindObjectsInactive.Include);
            settings.Open();
            Assert.IsFalse(MotionPreferences.ReduceMotion);
            settings.ReduceMotionButton.onClick.Invoke();
            Assert.IsTrue(MotionPreferences.ReduceMotion);
            StringAssert.Contains("On", settings.ReduceMotionButton.GetComponentInChildren<TMPro.TMP_Text>().text);
            settings.ReduceMotionButton.onClick.Invoke();
            Assert.IsFalse(MotionPreferences.ReduceMotion);
        }

        private static string BuildBakery(TownSceneController scene, BuildingDefinition bakeryDef)
        {
            var before = scene.Town.Buildings.Select(b => b.InstanceId).ToList();
            Assert.IsTrue(scene.Placement.BeginPlacement(bakeryDef));
            Assert.IsTrue(scene.Placement.Confirm());
            string id = scene.Town.Buildings.First(b => b.DefinitionId == "bakery" && !before.Contains(b.InstanceId)).InstanceId;
            Assert.IsTrue(scene.Construction.FinishNow(id));
            scene.Placement.Cancel();
            return id;
        }

        private static IEnumerator LoadTown()
        {
            SceneManager.LoadScene(SceneNames.Town);
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
