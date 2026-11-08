using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TownBuilder.Domain;

namespace TownBuilder.Tests
{
    public sealed class TrainTests
    {
        private static IReadOnlyList<TrainCrate> TwoCrates() => new[]
        {
            new TrainCrate("wheat", 4, 12, 2),
            new TrainCrate("bread", 1, 17, 3),
        };

        [Test]
        public void NewTrain_ArrivesOnFirstUpdate()
        {
            var train = new Train();
            Assert.IsFalse(train.IsAtStation);
            Assert.IsTrue(train.UpdateArrival(1000, TwoCrates));
            Assert.IsTrue(train.IsAtStation);
            Assert.AreEqual(2, train.Crates.Count);
            Assert.IsFalse(train.UpdateArrival(2000, TwoCrates), "already here");
        }

        [Test]
        public void Fill_TakesItems_AndOnlyOnce()
        {
            var train = new Train();
            train.UpdateArrival(0, TwoCrates);
            var inventory = new Inventory(50);
            inventory.TryAdd("wheat", 3);
            Assert.AreEqual(CrateFillResult.MissingItems, train.TryFill(0, inventory));
            inventory.TryAdd("wheat", 1);
            Assert.AreEqual(CrateFillResult.Filled, train.TryFill(0, inventory));
            Assert.AreEqual(0, inventory.Count("wheat"));
            Assert.AreEqual(CrateFillResult.AlreadyFilled, train.TryFill(0, inventory));
            Assert.AreEqual(CrateFillResult.NoSuchCrate, train.TryFill(5, inventory));
            Assert.AreEqual(1, train.FilledCount);
            Assert.IsFalse(train.AllFilled);
        }

        [Test]
        public void SendOff_TravelsThenReturnsWithNewCrates()
        {
            var train = new Train();
            train.UpdateArrival(0, TwoCrates);
            Assert.IsTrue(train.SendOff(1_000, 60_000));
            Assert.IsFalse(train.IsAtStation);
            Assert.AreEqual(0, train.Crates.Count);
            Assert.AreEqual(30_000, train.RemainingMs(31_000));
            Assert.IsFalse(train.UpdateArrival(60_999, TwoCrates));
            Assert.IsTrue(train.UpdateArrival(61_000, TwoCrates));
            Assert.AreEqual(0, train.FilledCount, "fresh crates");
            Assert.IsFalse(new Train().SendOff(0, 1), "can't send a train that isn't here");
        }

        [Test]
        public void ArriveNow_SkipsTheWait()
        {
            var train = new Train();
            train.UpdateArrival(0, TwoCrates);
            train.SendOff(0, 60_000);
            Assert.IsTrue(train.ArriveNow(10_000));
            Assert.IsTrue(train.UpdateArrival(10_000, TwoCrates));
        }

        [Test]
        public void Generator_ScalesAmountsByValue_AndPaysMoreThanSelling()
        {
            var candidates = new List<OrderCandidate> { new OrderCandidate("wheat", 2), new OrderCandidate("cake", 205) };
            var crates = TrainCrateGenerator.Generate(candidates, 200, 1.4, 4, new Random(3));
            Assert.AreEqual(200, crates.Count);
            foreach (var c in crates)
            {
                int unit = c.ItemId == "wheat" ? 2 : 205;
                if (c.ItemId == "wheat") Assert.That(c.Amount, Is.InRange(3, 6));
                else Assert.That(c.Amount, Is.InRange(1, 2));
                Assert.Greater(c.RewardCoins, unit * c.Amount, "crates pay more than the barn");
                Assert.GreaterOrEqual(c.RewardXp, 1);
            }

            Assert.IsTrue(crates.Any(c => c.ItemId == "wheat") && crates.Any(c => c.ItemId == "cake"));
            CollectionAssert.IsEmpty(TrainCrateGenerator.Generate(new List<OrderCandidate>(), 6, 1.4, 4, new Random(1)));
        }

        [Test]
        public void FullTrainBonus_IsAShareOfCrateCoins()
        {
            Assert.AreEqual(15, TrainCrateGenerator.FullTrainBonus(TwoCrates(), 0.5)); // (12 + 17) * 0.5 = 14.5, rounded up
        }

        [Test]
        public void Restore_KeepsState_AndFixesEmptyStation()
        {
            var train = new Train();
            train.Restore(TrainState.AtStation, 0, new[] { new TrainCrate("wheat", 2, 5, 1, filled: true) });
            Assert.IsTrue(train.IsAtStation);
            Assert.IsTrue(train.AllFilled);

            var empty = new Train();
            empty.Restore(TrainState.AtStation, 0, null);
            Assert.IsFalse(empty.IsAtStation, "a train with no crates comes back fresh");
        }
    }
}
