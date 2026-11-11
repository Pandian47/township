using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TownBuilder.Domain;

namespace TownBuilder.Tests
{
    public sealed class DeliveryTruckTests
    {
        private static IReadOnlyList<TruckLoad> TwoLoads() => new[]
        {
            new TruckLoad(new[] { new TruckLine("bread", 4), new TruckLine("egg", 2) }, 100, 10, "planks", 1),
            new TruckLoad(new[] { new TruckLine("wheat", 6) }, 40, 4, "nails", 1),
        };

        private static DeliveryTruck ParkedTruck()
        {
            var truck = new DeliveryTruck();
            Assert.IsNotNull(truck.UpdateReturn(0, TwoLoads), "a new truck is at the depot right away");
            return truck;
        }

        [Test]
        public void Loading_TakesAllLines_OrNothing()
        {
            var truck = ParkedTruck();
            var barn = new Inventory(100);
            barn.TryAdd("bread", 4);
            barn.TryAdd("egg", 1);

            Assert.AreEqual(TruckLoadResult.MissingItems, truck.TryLoad(0, barn));
            Assert.AreEqual(4, barn.Count("bread"), "nothing taken while a line is short");

            barn.TryAdd("egg", 1);
            Assert.IsTrue(truck.HasItemsFor(0, barn));
            Assert.AreEqual(TruckLoadResult.Loaded, truck.TryLoad(0, barn));
            Assert.AreEqual(0, barn.Count("bread"));
            Assert.AreEqual(0, barn.Count("egg"));
            Assert.AreEqual(TruckLoadResult.AlreadyLoaded, truck.TryLoad(0, barn));
            Assert.AreEqual(TruckLoadResult.NoSuchLoad, truck.TryLoad(5, barn));
        }

        [Test]
        public void NeedsOneLoad_ToLeave_AndPaysOnReturn()
        {
            var truck = ParkedTruck();
            Assert.IsFalse(truck.CanSend, "empty truck can't leave");
            Assert.IsFalse(truck.SendOff(0, 3_600_000, 0.25));

            var barn = new Inventory(100);
            barn.TryAdd("wheat", 6);
            truck.TryLoad(1, barn);
            Assert.IsTrue(truck.SendOff(1000, 3_600_000, 0.25));
            Assert.AreEqual(TruckState.Away, truck.State);
            Assert.AreEqual(40, truck.Cargo.Coins, "one load, no full-truck bonus");

            Assert.IsNull(truck.UpdateReturn(1000 + 3_599_999, TwoLoads), "still travelling");
            var payout = truck.UpdateReturn(1000 + 3_600_000, TwoLoads);
            Assert.IsNotNull(payout);
            Assert.AreEqual(40, payout.Coins);
            Assert.AreEqual(4, payout.Xp);
            Assert.AreEqual(1, payout.Materials["nails"]);
            Assert.IsTrue(truck.IsAtDepot);
            Assert.AreEqual(2, truck.Loads.Count, "new loads");
            Assert.IsTrue(truck.Cargo.IsEmpty);
        }

        [Test]
        public void FullTruck_AddsTheBonus()
        {
            var truck = ParkedTruck();
            var barn = new Inventory(100);
            barn.TryAdd("bread", 4);
            barn.TryAdd("egg", 2);
            barn.TryAdd("wheat", 6);
            truck.TryLoad(0, barn);
            truck.TryLoad(1, barn);
            Assert.IsTrue(truck.AllLoaded);
            Assert.AreEqual(35, truck.FullBonus(0.25), "25% of 140");

            truck.SendOff(0, 1000, 0.25);
            Assert.AreEqual(175, truck.Cargo.Coins);
            Assert.AreEqual(1, truck.Cargo.Materials["planks"]);
            Assert.AreEqual(1, truck.Cargo.Materials["nails"]);
        }

        [Test]
        public void ReturnNow_BringsItBackEarly_WithItsCargo()
        {
            var truck = ParkedTruck();
            var barn = new Inventory(100);
            barn.TryAdd("wheat", 6);
            truck.TryLoad(1, barn);
            truck.SendOff(0, 3_600_000, 0.25);
            Assert.IsTrue(truck.ReturnNow(60_000));
            Assert.AreEqual(40, truck.UpdateReturn(60_000, TwoLoads).Coins);
            Assert.IsFalse(truck.ReturnNow(60_000), "already back");
        }

        [Test]
        public void Restore_KeepsCargo_AndAnEmptyParkedTruckComesBackFresh()
        {
            var cargo = new TruckPayout { Coins = 77, Xp = 7 };
            var truck = new DeliveryTruck();
            truck.Restore(TruckState.Away, 5000, null, cargo);
            Assert.AreEqual(77, truck.Cargo.Coins);

            var parked = new DeliveryTruck();
            parked.Restore(TruckState.AtDepot, 0, new List<TruckLoad>(), null);
            Assert.AreEqual(TruckState.Away, parked.State);
        }

        [Test]
        public void Generator_MixesItems_BiggerAmountsForCheapGoods()
        {
            var candidates = new List<OrderCandidate> { new OrderCandidate("wheat", 2), new OrderCandidate("bread", 12), new OrderCandidate("cake", 205) };
            var loads = TruckLoadGenerator.Generate(candidates, 3, 2, 1.6, 6, new System.Random(1), new[] { "planks" });
            Assert.AreEqual(3, loads.Count);
            foreach (var load in loads)
            {
                Assert.AreEqual(2, load.Lines.Count);
                Assert.AreEqual(2, load.Lines.Select(l => l.ItemId).Distinct().Count(), "two different items");
                Assert.AreEqual("planks", load.MaterialId);
                foreach (var line in load.Lines.Where(l => l.ItemId == "wheat")) Assert.GreaterOrEqual(line.Amount, 5);
                foreach (var line in load.Lines.Where(l => l.ItemId == "cake")) Assert.LessOrEqual(line.Amount, 3);
                Assert.Greater(load.RewardCoins, 0);
            }
        }
    }
}
