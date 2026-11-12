using NUnit.Framework;
using TownBuilder.Domain;

namespace TownBuilder.Tests
{
    /// <summary>v0.40.0: buildings have two facings; turning swaps the footprint's X and Y.</summary>
    public sealed class RotateTests
    {
        private static Town NewTown() => new Town(new GridRect(0, 0, 10, 10));

        [Test]
        public void SizeFor_SwapsWhenFlipped()
        {
            Assert.AreEqual(new GridCoord(2, 3), PlacedBuilding.SizeFor(new GridCoord(2, 3), false));
            Assert.AreEqual(new GridCoord(3, 2), PlacedBuilding.SizeFor(new GridCoord(2, 3), true));
        }

        [Test]
        public void Add_Flipped_OccupiesTheTurnedFootprint()
        {
            var town = NewTown();
            Assert.AreEqual(PlacementResult.Valid, town.TryAdd("a", "shed", new GridCoord(0, 0), new GridCoord(2, 3), out var b, flipped: true));
            Assert.IsTrue(b.Flipped);
            Assert.AreEqual(new GridCoord(3, 2), b.Size);
            Assert.IsTrue(town.TryGetAt(new GridCoord(2, 1), out _), "3 wide along X");
            Assert.IsFalse(town.TryGetAt(new GridCoord(1, 2), out _), "only 2 deep along Y");
        }

        [Test]
        public void Rotate_InPlace_SwapsTheFootprint_AndBack()
        {
            var town = NewTown();
            town.TryAdd("a", "shed", new GridCoord(1, 1), new GridCoord(2, 3), out var b);
            Assert.AreEqual(PlacementResult.Valid, town.TryRotate("a"));
            Assert.IsTrue(b.Flipped);
            Assert.AreEqual(new GridCoord(3, 2), b.Size);
            Assert.AreEqual(new GridCoord(1, 1), b.Origin, "turns where it stands");
            Assert.IsTrue(town.TryGetAt(new GridCoord(3, 1), out _));
            Assert.IsFalse(town.TryGetAt(new GridCoord(1, 3), out _), "the old cells are free again");

            Assert.AreEqual(PlacementResult.Valid, town.TryRotate("a"));
            Assert.IsFalse(b.Flipped);
            Assert.AreEqual(new GridCoord(2, 3), b.Size);
        }

        [Test]
        public void Rotate_Refused_WhenTheTurnedFootprintIsBlocked()
        {
            var town = NewTown();
            town.TryAdd("a", "shed", new GridCoord(1, 1), new GridCoord(2, 3), out var shed);
            town.TryAdd("b", "hut", new GridCoord(3, 1), new GridCoord(1, 1), out _);
            Assert.AreEqual(PlacementResult.Overlap, town.TryRotate("a"));
            Assert.IsFalse(shed.Flipped, "nothing changed");
            Assert.AreEqual(new GridCoord(2, 3), shed.Size);
        }

        [Test]
        public void Rotate_SquareBuilding_AlwaysFits()
        {
            var town = NewTown();
            town.TryAdd("mill", "feed_mill", new GridCoord(4, 4), new GridCoord(2, 2), out var mill);
            town.TryAdd("n", "hut", new GridCoord(6, 4), new GridCoord(1, 1), out _);
            Assert.AreEqual(PlacementResult.Valid, town.TryRotate("mill"));
            Assert.IsTrue(mill.Flipped);
            Assert.AreEqual(new GridCoord(2, 2), mill.Size);
        }

        [Test]
        public void Move_CanChangeTheFacing()
        {
            var town = NewTown();
            town.TryAdd("a", "shed", new GridCoord(0, 0), new GridCoord(2, 3), out var b);
            int moved = 0;
            town.BuildingMoved += _ => moved++;
            Assert.AreEqual(PlacementResult.Valid, town.TryMove("a", new GridCoord(5, 5), true));
            Assert.AreEqual(1, moved);
            Assert.IsTrue(b.Flipped);
            Assert.AreEqual(new GridCoord(5, 5), b.Origin);
            Assert.AreEqual(new GridCoord(3, 2), b.Size);
            Assert.AreEqual(PlacementResult.Valid, town.TryMove("a", new GridCoord(5, 5)), "null keeps the facing");
            Assert.IsTrue(b.Flipped);
        }
    }
}
