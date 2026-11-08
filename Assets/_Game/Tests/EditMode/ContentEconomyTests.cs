using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

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
