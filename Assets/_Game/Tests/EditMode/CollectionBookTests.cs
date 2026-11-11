using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TownBuilder.Domain;

namespace TownBuilder.Tests
{
    public sealed class CollectionBookTests
    {
        private static CollectionBook NewBook() => new CollectionBook(new[]
        {
            new CollectionSetSpec("meadow", AchievementKind.HarvestCrops, 0.1, new[] { "clover", "acorn", "feather" }),
            new CollectionSetSpec("market", AchievementKind.DeliverOrders, 1.0, new[] { "coin", "stamp" }),
        });

        [Test]
        public void CompletingASet_UsesOneOfEach_AndCanRepeat()
        {
            var book = NewBook();
            book.Add("clover", 2);
            book.Add("acorn");
            Assert.AreEqual(2, book.DistinctOwned("meadow"));
            Assert.IsFalse(book.CanComplete("meadow"));
            Assert.IsFalse(book.TryComplete("meadow"));

            book.Add("feather");
            Assert.IsTrue(book.CanComplete("meadow"));
            Assert.AreEqual(1, book.CompletableCount);
            Assert.IsTrue(book.TryComplete("meadow"));
            Assert.AreEqual(1, book.Count("clover"), "the spare one stays");
            Assert.AreEqual(0, book.Count("acorn"));
            Assert.AreEqual(1, book.Completions("meadow"));
            Assert.IsFalse(book.CanComplete("meadow"));

            book.Add("acorn");
            book.Add("feather");
            Assert.IsTrue(book.TryComplete("meadow"));
            Assert.AreEqual(2, book.Completions("meadow"));
        }

        [Test]
        public void Roll_OnlyFromSetsOfThatActivity_AtTheirChance()
        {
            var book = NewBook();
            var random = new Random(7);
            for (int i = 0; i < 50; i++) Assert.IsNotNull(book.Roll(AchievementKind.DeliverOrders, random), "chance 1: always");
            Assert.AreEqual(50, book.Count("coin") + book.Count("stamp"));
            Assert.Greater(book.Count("coin"), 0);
            Assert.Greater(book.Count("stamp"), 0, "both items can drop");
            Assert.IsNull(book.Roll(AchievementKind.LoadCrates, random), "no collection for crates");

            int found = 0;
            for (int i = 0; i < 4000; i++)
                if (book.Roll(AchievementKind.HarvestCrops, random) != null) found++;
            Assert.AreEqual(400, found, 60, "about 10 %");
        }

        [Test]
        public void Restore_DropsUnknownItemsAndSets()
        {
            var book = NewBook();
            book.Restore(
                new[] { new KeyValuePair<string, int>("clover", 3), new KeyValuePair<string, int>("gone", 5) },
                new[] { new KeyValuePair<string, int>("market", 2), new KeyValuePair<string, int>("old_set", 1) });
            Assert.AreEqual(3, book.Count("clover"));
            Assert.AreEqual(0, book.Count("gone"));
            Assert.AreEqual(2, book.Completions("market"));
            Assert.AreEqual(0, book.Completions("old_set"));
            Assert.AreEqual(1, book.AllCompletions.Count(), "only the known set");
        }

        [Test]
        public void Spec_NeedsAnIdAndItems()
        {
            Assert.Throws<ArgumentException>(() => new CollectionSetSpec("", AchievementKind.HarvestCrops, 0.1, new[] { "a" }));
            Assert.Throws<ArgumentException>(() => new CollectionSetSpec("x", AchievementKind.HarvestCrops, 0.1, new string[0]));
            Assert.AreEqual(1.0, new CollectionSetSpec("x", AchievementKind.HarvestCrops, 5, new[] { "a" }).DropChance);
        }
    }
}
