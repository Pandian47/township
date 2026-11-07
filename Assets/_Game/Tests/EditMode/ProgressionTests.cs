using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TownBuilder.Domain;

namespace TownBuilder.Tests
{
    public sealed class LevelCurveTests
    {
        private static readonly LevelCurve Curve = new LevelCurve(new[] { 10, 20, 30 }, extraPerLevelAfterTable: 5);

        [Test]
        public void Thresholds_AreCumulative()
        {
            Assert.AreEqual(0, Curve.TotalXpForLevel(1));
            Assert.AreEqual(10, Curve.TotalXpForLevel(2));
            Assert.AreEqual(30, Curve.TotalXpForLevel(3));
            Assert.AreEqual(60, Curve.TotalXpForLevel(4));
        }

        [Test]
        public void BeyondTable_GrowsByExtra()
        {
            Assert.AreEqual(35, Curve.XpToNext(4));
            Assert.AreEqual(40, Curve.XpToNext(5));
        }

        [TestCase(0, 1)]
        [TestCase(9, 1)]
        [TestCase(10, 2)]
        [TestCase(29, 2)]
        [TestCase(30, 3)]
        [TestCase(1000, 99)]
        public void LevelForTotalXp(long xp, int expected)
        {
            if (expected == 99) Assert.Greater(Curve.LevelForTotalXp(xp), 10);
            else Assert.AreEqual(expected, Curve.LevelForTotalXp(xp));
        }
    }

    public sealed class PlayerProgressTests
    {
        private static LevelCurve Curve => new LevelCurve(new[] { 10, 20, 30 });

        [Test]
        public void AddXp_LevelsUp_AndRaisesEventPerLevel()
        {
            var p = new PlayerProgress(1, 0, Curve);
            var levels = new List<int>();
            p.LeveledUp += l => levels.Add(l);

            p.AddXp(35); // crosses 10 and 30
            Assert.AreEqual(3, p.Level);
            CollectionAssert.AreEqual(new[] { 2, 3 }, levels);
            Assert.AreEqual(5, p.XpIntoLevel);
            Assert.AreEqual(30, p.XpForNextLevel);
        }

        [Test]
        public void LoadedLevelBehindXp_IsRepaired_AheadIsKept()
        {
            Assert.AreEqual(3, new PlayerProgress(1, 35, Curve).Level);
            Assert.AreEqual(5, new PlayerProgress(5, 0, Curve).Level, "never take levels away");
        }

        [Test]
        public void WithoutCurve_LevelNeverChanges()
        {
            var p = new PlayerProgress(1, 0);
            p.AddXp(10_000);
            Assert.AreEqual(1, p.Level);
        }
    }

    public sealed class OrderTests
    {
        private static readonly OrderCandidate[] Candidates =
        {
            new OrderCandidate("wheat", 2), new OrderCandidate("corn", 4), new OrderCandidate("bread", 12),
        };

        [Test]
        public void Generator_ProducesValidOrders()
        {
            var gen = new OrderGenerator();
            var rng = new Random(42);
            for (int i = 0; i < 200; i++)
            {
                var order = gen.Generate(Candidates, playerLevel: 4, rng);
                Assert.That(order.Lines.Count, Is.InRange(1, 3));
                Assert.AreEqual(order.Lines.Count, order.Lines.Select(l => l.ItemId).Distinct().Count(), "items are distinct");
                Assert.IsTrue(order.Lines.All(l => l.Amount >= 1));

                long sellValue = order.Lines.Sum(l => (long)Candidates.First(c => c.ItemId == l.ItemId).UnitValue * l.Amount);
                Assert.Greater(order.RewardCoins, sellValue, "orders must pay more than selling the items");
                Assert.GreaterOrEqual(order.RewardXp, 1);
            }
        }

        [Test]
        public void Generator_LowLevel_UsesSingleLine()
        {
            var gen = new OrderGenerator();
            var rng = new Random(1);
            for (int i = 0; i < 50; i++) Assert.AreEqual(1, gen.Generate(Candidates, 1, rng).Lines.Count);
        }

        [Test]
        public void Deliver_TakesItems_PaysReward_EmptiesSlot()
        {
            var board = new OrderBoard(3);
            board.Set(0, new Order(new[] { new OrderLine("bread", 2), new OrderLine("wheat", 3) }, 60, 8));
            var inv = new Inventory(50);
            inv.TryAdd("bread", 2);
            inv.TryAdd("wheat", 5);
            var wallet = new Wallet(0, 0);
            var progress = new PlayerProgress(1, 0);

            Assert.AreEqual(DeliverResult.Delivered, board.TryDeliver(0, inv, wallet, progress, 0));
            Assert.AreEqual(0, inv.Count("bread"));
            Assert.AreEqual(2, inv.Count("wheat"));
            Assert.AreEqual(60, wallet.Coins);
            Assert.AreEqual(8, progress.Xp);
            Assert.IsNull(board.Get(0));
        }

        [Test]
        public void Deliver_MissingItems_ChangesNothing()
        {
            var board = new OrderBoard(1);
            board.Set(0, new Order(new[] { new OrderLine("bread", 2) }, 60, 8));
            var inv = new Inventory(50);
            inv.TryAdd("bread", 1);
            var wallet = new Wallet(0, 0);

            Assert.AreEqual(DeliverResult.MissingItems, board.TryDeliver(0, inv, wallet, new PlayerProgress(1, 0), 0));
            Assert.AreEqual(1, inv.Count("bread"));
            Assert.AreEqual(0, wallet.Coins);
            Assert.IsNotNull(board.Get(0));
        }

        [Test]
        public void SkippedOrder_IsUnavailableUntilCooldownEnds()
        {
            var board = new OrderBoard(1);
            board.Set(0, new Order(new[] { new OrderLine("wheat", 1) }, 10, 1, availableAtUnixMs: 60_000));
            var inv = new Inventory(50);
            inv.TryAdd("wheat", 1);

            Assert.IsFalse(board.CanDeliver(0, inv, 59_999));
            Assert.AreEqual(DeliverResult.NotAvailable, board.TryDeliver(0, inv, new Wallet(0, 0), new PlayerProgress(1, 0), 30_000));
            Assert.IsTrue(board.CanDeliver(0, inv, 60_000));
        }

        [Test]
        public void Mapper_RoundTripsOrders()
        {
            var board = new OrderBoard(3);
            board.Set(1, new Order(new[] { new OrderLine("bread", 2), new OrderLine("wheat", 3) }, 60, 8, 1234));
            var save = NewGameFactory.Create(0, 0, 0);
            TownSaveMapper.WriteOrders(save, board);

            Assert.AreEqual(1, save.orders.Count);
            Assert.AreEqual(1, save.orders[0].slot);
            CollectionAssert.AreEqual(new[] { "bread", "wheat" }, save.orders[0].itemIds);
            CollectionAssert.AreEqual(new[] { 2, 3 }, save.orders[0].amounts);
            Assert.AreEqual(1234, save.orders[0].availableAtUnixMs);
        }
    }
}
