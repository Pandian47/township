using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TownBuilder.Tests
{
    /// <summary>
    /// Economy rules checked on the real content, so new crops, factories and recipes can't break the game's logic
    /// (see Documentation/GameEconomy.md).
    /// </summary>
    public sealed class ContentEconomyTests
    {
        private const string ContentPath = "Assets/_Game/ScriptableObjects/GameContent.asset";
        private GameContent _content;

        [SetUp]
        public void SetUp()
        {
            _content = AssetDatabase.LoadAssetAtPath<GameContent>(ContentPath);
            Assert.NotNull(_content, "Run TownBuilder > Setup > Run Full Setup");
        }

        [Test]
        public void Processing_AlwaysBeatsSellingTheIngredients()
        {
            foreach (var recipe in _content.Recipes)
            {
                int raw = recipe.Ingredients.Sum(i => i.item.SellPrice * i.amount);
                int processed = recipe.Output.SellPrice * recipe.OutputAmount;
                Assert.Greater(processed, raw, $"{recipe.Id}: output worth {processed}, ingredients sell for {raw}");
            }
        }

        [Test]
        public void EveryRecipe_IsMadeByExactlyOneFactory()
        {
            foreach (var recipe in _content.Recipes)
            {
                var factories = _content.Buildings.Where(b => b.Recipes != null && b.Recipes.Contains(recipe)).ToList();
                Assert.AreEqual(1, factories.Count, $"{recipe.Id} is in {factories.Count} factories");
            }
        }

        [Test]
        public void Recipes_UnlockNoEarlierThan_TheirFactoryAndIngredients()
        {
            var itemUnlock = ItemUnlockLevels();
            foreach (var factory in _content.Buildings.Where(b => b.IsFactory))
            foreach (var recipe in factory.Recipes)
            {
                Assert.GreaterOrEqual(recipe.UnlockLevel, factory.UnlockLevel, $"{recipe.Id} unlocks before {factory.Id}");
                foreach (var i in recipe.Ingredients)
                {
                    Assert.IsTrue(itemUnlock.ContainsKey(i.item.Id), $"{recipe.Id}: nothing produces {i.item.Id}");
                    Assert.GreaterOrEqual(recipe.UnlockLevel, itemUnlock[i.item.Id],
                        $"{recipe.Id} unlocks at {recipe.UnlockLevel} but {i.item.Id} only at {itemUnlock[i.item.Id]}");
                }
            }
        }

        [Test]
        public void FeedMill_MakesChickenAndCowFeed_FromAvailableCrops()
        {
            Assert.IsTrue(_content.TryGetBuilding("feed_mill", out var mill));
            Assert.IsTrue(mill.IsFactory);
            CollectionAssert.AreEquivalent(new[] { "chicken_feed", "cow_feed" }, mill.Recipes.Select(r => r.Output.Id).ToArray());
            Assert.IsTrue(_content.TryGetCrop("soybean", out var soybean));
            Assert.AreEqual(mill.UnlockLevel, soybean.UnlockLevel, "Soybean arrives together with the Feed Mill");
        }

        [Test]
        public void AnimalBuildings_TurnFeedIntoAnimalProducts()
        {
            Assert.IsTrue(_content.TryGetBuilding("chicken_coop", out var coop));
            Assert.IsTrue(_content.TryGetBuilding("dairy_shed", out var dairy));
            Assert.AreEqual("egg", coop.Recipes.Single().Output.Id);
            Assert.AreEqual("chicken_feed", coop.Recipes.Single().Ingredients.Single().item.Id);
            Assert.AreEqual("milk", dairy.Recipes.Single().Output.Id);
            Assert.AreEqual("cow_feed", dairy.Recipes.Single().Ingredients.Single().item.Id);
        }

        [Test]
        public void SugarMill_MakesSugar_FromSugarcane()
        {
            Assert.IsTrue(_content.TryGetBuilding("sugar_mill", out var mill));
            var recipe = mill.Recipes.Single();
            Assert.AreEqual("sugar", recipe.Output.Id);
            Assert.AreEqual("sugarcane", recipe.Ingredients.Single().item.Id);
            Assert.IsTrue(_content.TryGetCrop("sugarcane", out var cane));
            Assert.AreEqual(mill.UnlockLevel, cane.UnlockLevel, "Sugarcane arrives together with the Sugar Mill");
        }

        [Test]
        public void Sweets_And_Dairy_UseEggsMilkAndSugar()
        {
            Assert.IsTrue(_content.TryGetBuilding("bakery", out var bakery));
            Assert.IsTrue(_content.TryGetBuilding("creamery", out var creamery));
            CollectionAssert.IsSubsetOf(new[] { "bread", "corn_bread", "cookies", "cake" }, bakery.Recipes.Select(r => r.Output.Id).ToArray(),
                "the Bakery keeps its first recipes and gains cookies and cake");
            CollectionAssert.AreEquivalent(new[] { "butter", "cheese" }, creamery.Recipes.Select(r => r.Output.Id).ToArray());

            _content.TryGetRecipe("bake_cake", out var cake);
            CollectionAssert.AreEquivalent(new[] { "wheat", "egg", "milk", "sugar" }, cake.Ingredients.Select(i => i.item.Id).ToArray());
        }

        [Test]
        public void Tasks_AreValid_AndAchievableAtTheirLevel()
        {
            var itemUnlock = ItemUnlockLevels();
            var ids = new HashSet<string>();
            foreach (var task in _content.Tasks)
            {
                Assert.NotNull(task, "empty entry in GameContent.Tasks");
                Assert.IsTrue(ids.Add(task.Id), $"duplicate task id {task.Id}");
                Assert.Greater(task.RewardCoins + task.RewardXp + task.RewardGems, 0, $"{task.Id} has no reward");
                switch (task.Kind)
                {
                    case TownBuilder.Domain.TaskKind.Build:
                        Assert.NotNull(task.TargetBuilding, $"{task.Id}: no building");
                        Assert.GreaterOrEqual(task.UnlockLevel, task.TargetBuilding.UnlockLevel, $"{task.Id} appears before its building unlocks");
                        break;
                    case TownBuilder.Domain.TaskKind.Harvest:
                    case TownBuilder.Domain.TaskKind.Collect:
                        if (task.TargetItem == null) break;
                        Assert.IsTrue(itemUnlock.ContainsKey(task.TargetItem.Id), $"{task.Id}: nothing makes {task.TargetItem.Id}");
                        Assert.GreaterOrEqual(task.UnlockLevel, itemUnlock[task.TargetItem.Id], $"{task.Id} appears before {task.TargetItem.Id} can be made");
                        break;
                }
            }

            int startTasks = _content.Tasks.Count(t => t.UnlockLevel == 1);
            Assert.GreaterOrEqual(startTasks, _content.TaskSlots, "a new player's task board starts full");
        }

        [Test]
        public void Materials_Exist_AreSellable_AndNeverRequested()
        {
            var ids = _content.Materials.Select(m => m.Id).ToList();
            CollectionAssert.AreEquivalent(new[] { "planks", "bricks", "nails" }, ids);
            foreach (var m in _content.Materials)
            {
                Assert.IsTrue(_content.TryGetItem(m.Id, out var item) && item == m, $"{m.Id} findable as an item");
                Assert.Greater(m.SellPrice, 0);
                Assert.NotNull(m.Icon, $"{m.Id} icon");
            }

            Assert.IsFalse(_content.Recipes.Any(r => ids.Contains(r.Output.Id)), "no factory makes materials");
            Assert.IsTrue(_content.Tasks.Any(t => t.RewardMaterialAmount > 0 && t.UnlockLevel <= 3), "materials obtainable before the train");
        }

        [Test]
        public void LoadCrateTasks_AppearOnlyOnceTheStationExists()
        {
            _content.TryGetBuilding(_content.TrainStationId, out var station);
            foreach (var t in _content.Tasks.Where(t => t.Kind == TownBuilder.Domain.TaskKind.LoadCrates))
                Assert.GreaterOrEqual(t.UnlockLevel, station.UnlockLevel, t.Id);
        }

        [Test]
        public void LandTiers_AreConfigured_AndGetPricierFurtherOut()
        {
            Assert.Greater(_content.LandTiers.Count, 0, "land prices");
            int lastLevel = 0, lastCoins = 0;
            foreach (var tier in _content.LandTiers)
            {
                Assert.GreaterOrEqual(tier.unlockLevel, lastLevel);
                Assert.GreaterOrEqual(tier.coins, lastCoins);
                Assert.IsTrue(tier.materials.All(m => m.material != null && m.amount > 0), "every material cost is set");
                lastLevel = tier.unlockLevel;
                lastCoins = tier.coins;
            }

            Assert.IsTrue(_content.Tasks.Any(t => t.RewardMaterialAmount > 0 && t.UnlockLevel <= _content.LandTiers[0].unlockLevel),
                "materials for the first plots can be earned by the time they unlock");
        }

        [Test]
        public void ChimneyPoints_LieOnTheSprite()
        {
            foreach (var b in _content.Buildings.Where(b => b.HasChimney))
            {
                var p = b.ChimneyPoint;
                Assert.That(p.x, Is.InRange(0f, 1f), $"{b.Id} chimney x");
                Assert.That(p.y, Is.InRange(0.5f, 1f), $"{b.Id} chimney should be in the upper half of the sprite");
            }

            Assert.IsTrue(_content.Buildings.Any(b => b.IsFactory && b.HasChimney), "at least one factory smokes");
        }

        [Test]
        public void DepthSorting_IsCustomAxis_AlongY()
        {
            var renderer = AssetDatabase.LoadMainAssetAtPath("Assets/_Game/Settings/Renderer2D.asset");
            Assert.NotNull(renderer, "2D renderer asset");
            var so = new SerializedObject(renderer);
            Assert.AreEqual(3, so.FindProperty("m_TransparencySortMode").intValue,
                "Transparency Sort Mode must be Custom Axis, or buildings, trains and people overlap in random order");
            Assert.AreEqual(new Vector3(0f, 1f, 0f), so.FindProperty("m_TransparencySortAxis").vector3Value);
        }

        [Test]
        public void TrainStation_IsConfigured()
        {
            Assert.IsTrue(_content.TryGetBuilding(_content.TrainStationId, out var station), "train station building");
            Assert.AreEqual(1, station.MaxCount, "only one station");
            Assert.IsFalse(station.Movable, "fixed landmark");
            Assert.IsFalse(station.ShowInShop, "not sold in the shop");
            Assert.IsFalse(station.Removable);
            Assert.AreEqual(5, station.UnlockLevel, "opens at level 5");
            Assert.IsTrue(_content.StarterBuildings.Any(s => s.building == station), "every town starts with it");
            Assert.Greater(_content.TrainCrates, 0);
            Assert.Greater(_content.TrainCoinMultiplier, 1f, "train crates must pay more than the barn");
        }

        /// <summary>Earliest level at which each item can be obtained (crops: their unlock; products: their cheapest recipe).</summary>
        private Dictionary<string, int> ItemUnlockLevels()
        {
            var levels = _content.Crops.ToDictionary(c => c.Id, c => c.UnlockLevel);
            foreach (var factory in _content.Buildings.Where(b => b.IsFactory))
            foreach (var recipe in factory.Recipes)
            {
                int level = System.Math.Max(recipe.UnlockLevel, factory.UnlockLevel);
                string id = recipe.Output.Id;
                levels[id] = levels.TryGetValue(id, out var existing) ? System.Math.Min(existing, level) : level;
            }

            return levels;
        }
    }
}
