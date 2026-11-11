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
    /// <summary>Milestone 6 step 5: decorations and the shop tabs.</summary>
    public sealed class DecorationsFlowTests
    {
        private string _saveDir;

        [SetUp]
        public void SetUp()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            _saveDir = Path.Combine(Path.GetTempPath(), "TownBuilderDecoTests", System.Guid.NewGuid().ToString("N"));
            GameRoot.SaveDirectoryOverride = _saveDir;
        }

        [TearDown]
        public void TearDown()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            GameRoot.SaveDirectoryOverride = null;
            if (Directory.Exists(_saveDir)) Directory.Delete(_saveDir, true);
        }

        [UnityTest]
        public IEnumerator ShopTabs_SplitBuildingsAndDecorations()
        {
            yield return LoadTown();
            var content = GameRoot.Current.Context.Content;
            var shop = Object.FindAnyObjectByType<BuildingShopPanel>();
            GameObject.Find("BuildButton").GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(shop.IsOpen);
            Assert.AreEqual(BuildingCategory.Residential, shop.CurrentCategory, "opens on Houses");
            Assert.IsFalse(shop.ShowingDecorations);

            // One tab per category with something for sale; each tab shows exactly that category.
            var expected = BuildingShopPanel.TabOrder.Where(c => content.Buildings.Any(b => b.ShowInShop && b.Category == c)).ToList();
            CollectionAssert.AreEqual(expected, shop.Categories.ToList());
            int total = 0;
            foreach (var category in expected)
            {
                GameObject.Find($"Tab_{category}").GetComponent<Button>().onClick.Invoke();
                Assert.AreEqual(category, shop.CurrentCategory);
                int count = content.Buildings.Count(b => b.ShowInShop && b.Category == category);
                Assert.AreEqual(count, shop.VisibleItems, $"{category} tab");
                total += count;
            }

            Assert.AreEqual(content.Buildings.Count(b => b.ShowInShop), total, "every shop item is in one tab");

            // Factories tab: only factories. Fruit trees are on the Farm tab.
            Assert.IsTrue(content.Buildings.Where(b => b.ShowInShop && b.Category == BuildingCategory.Production).All(b => b.IsFactory));
            Assert.IsTrue(content.Buildings.Where(b => b.IsFruitTree).All(b => b.Category == BuildingCategory.Farming));
            GameObject.Find("Tab_Decoration").GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(shop.ShowingDecorations);
        }

        [UnityTest]
        public IEnumerator Trees_AreInstant_Unlimited_Movable_AndSaved()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            GameRoot.Current.Context.Content.TryGetBuilding("tree", out var tree);
            Assert.AreEqual(BuildingCategory.Decoration, tree.Category);

            scene.Wallet.AddCoins(1_000);
            for (int i = 0; i < 4; i++)
            {
                Assert.IsTrue(scene.Placement.BeginPlacement(tree), $"tree {i + 1}");
                Assert.IsTrue(scene.Placement.Confirm());
            }

            var trees = scene.Town.Buildings.Where(b => b.DefinitionId == "tree").ToList();
            Assert.AreEqual(4, trees.Count, "no limit");
            Assert.IsTrue(trees.All(t => scene.Construction.IsBuilt(t.InstanceId)), "no construction time");

            scene.Placement.SelectBuilding(trees[0].InstanceId);
            Assert.IsTrue(scene.Placement.BeginMoveSelected(), "decorations can be moved");
            scene.Placement.Cancel();

            scene.SaveNow();
            Object.DestroyImmediate(GameRoot.Current.gameObject);
            yield return null;
            SceneManager.LoadScene(SceneNames.MainMenu);
            float end = Time.realtimeSinceStartup + 5f;
            while (GameRoot.Current == null && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(GameRoot.Current.Context.ContinueGame().Succeeded);
            yield return LoadTown();
            scene = Object.FindAnyObjectByType<TownSceneController>();
            Assert.AreEqual(4, scene.Town.CountOf("tree"));
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
