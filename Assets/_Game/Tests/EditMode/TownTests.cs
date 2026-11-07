using NUnit.Framework;
using TownBuilder.Domain;

namespace TownBuilder.Tests
{
    public sealed class TownTests
    {
        private static readonly GridCoord Size2 = new GridCoord(2, 2);

        private static Town NewTown(params GridCoord[] blocked) => new Town(new GridRect(0, 0, 10, 10), blocked);

        [Test]
        public void Place_OnFreeCells_Succeeds()
        {
            var town = NewTown();
            Assert.AreEqual(PlacementResult.Valid, town.TryAdd("a", "barn", new GridCoord(1, 1), Size2, out var b));
            Assert.NotNull(b);
            Assert.AreEqual(4, town.Occupancy.OccupiedCellCount);
            Assert.IsTrue(town.TryGetAt(new GridCoord(2, 2), out var at));
            Assert.AreEqual("a", at.InstanceId);
        }

        [Test]
        public void Place_Overlapping_IsRejected()
        {
            var town = NewTown();
            town.TryAdd("a", "barn", new GridCoord(1, 1), Size2, out _);
            Assert.AreEqual(PlacementResult.Overlap, town.TryAdd("b", "barn", new GridCoord(2, 2), Size2, out _));
            Assert.AreEqual(1, town.BuildingCount);
        }

        [Test]
        public void Place_Adjacent_IsAllowed()
        {
            var town = NewTown();
            town.TryAdd("a", "barn", new GridCoord(1, 1), Size2, out _);
            Assert.AreEqual(PlacementResult.Valid, town.TryAdd("b", "barn", new GridCoord(3, 1), Size2, out _));
        }

        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        [TestCase(9, 0)]
        [TestCase(0, 9)]
        public void Place_OutsideBounds_IsRejected(int x, int y)
        {
            Assert.AreEqual(PlacementResult.OutOfBounds, NewTown().CanPlace(new GridCoord(x, y), Size2));
        }

        [Test]
        public void Place_OnBlockedTerrain_IsRejected()
        {
            var town = NewTown(new GridCoord(5, 5));
            Assert.AreEqual(PlacementResult.BlockedTerrain, town.CanPlace(new GridCoord(4, 4), Size2));
            Assert.AreEqual(PlacementResult.Valid, town.CanPlace(new GridCoord(6, 6), Size2));
        }

        [Test]
        public void Place_DuplicateInstanceId_IsRejected()
        {
            var town = NewTown();
            town.TryAdd("a", "barn", new GridCoord(0, 0), Size2, out _);
            Assert.AreEqual(PlacementResult.DuplicateId, town.TryAdd("a", "barn", new GridCoord(5, 5), Size2, out _));
        }

        [Test]
        public void Place_ZeroSize_IsRejected()
        {
            Assert.AreEqual(PlacementResult.InvalidSize, NewTown().CanPlace(new GridCoord(1, 1), new GridCoord(0, 2)));
        }

        [Test]
        public void Move_OntoOwnPartialFootprint_Succeeds()
        {
            var town = NewTown();
            town.TryAdd("a", "barn", new GridCoord(1, 1), Size2, out var b);
            Assert.AreEqual(PlacementResult.Valid, town.TryMove("a", new GridCoord(2, 1)));
            Assert.AreEqual(new GridCoord(2, 1), b.Origin);
            Assert.IsFalse(town.TryGetAt(new GridCoord(1, 1), out _), "old cell should be released");
            Assert.AreEqual(4, town.Occupancy.OccupiedCellCount);
        }

        [Test]
        public void Move_OntoOtherBuilding_IsRejectedAndKeepsPosition()
        {
            var town = NewTown();
            town.TryAdd("a", "barn", new GridCoord(0, 0), Size2, out var a);
            town.TryAdd("b", "barn", new GridCoord(4, 4), Size2, out _);
            Assert.AreEqual(PlacementResult.Overlap, town.TryMove("a", new GridCoord(3, 3)));
            Assert.AreEqual(new GridCoord(0, 0), a.Origin);
        }

        [Test]
        public void Remove_ReleasesCells_AndRaisesEvent()
        {
            var town = NewTown();
            town.TryAdd("a", "barn", new GridCoord(1, 1), Size2, out _);
            PlacedBuilding removed = null;
            town.BuildingRemoved += b => removed = b;

            Assert.IsTrue(town.Remove("a"));
            Assert.AreEqual("a", removed?.InstanceId);
            Assert.AreEqual(0, town.Occupancy.OccupiedCellCount);
            Assert.IsFalse(town.Remove("a"));
        }

        [Test]
        public void CountOf_CountsByDefinition()
        {
            var town = NewTown();
            town.TryAdd("a", "barn", new GridCoord(0, 0), Size2, out _);
            town.TryAdd("b", "barn", new GridCoord(4, 4), Size2, out _);
            town.TryAdd("c", "silo", new GridCoord(8, 8), GridCoord.One, out _);
            Assert.AreEqual(2, town.CountOf("barn"));
            Assert.AreEqual(1, town.CountOf("silo"));
            Assert.AreEqual(0, town.CountOf("bakery"));
        }

        [Test]
        public void ClampOrigin_KeepsFootprintInside()
        {
            var rect = new GridRect(0, 0, 10, 10);
            Assert.AreEqual(new GridCoord(7, 0), rect.ClampOrigin(new GridCoord(12, -3), new GridCoord(3, 3)));
        }
    }
}
