using System.Linq;
using NUnit.Framework;
using TownBuilder.Domain;

namespace TownBuilder.Tests
{
    public sealed class LandPlotsTests
    {
        // 36x36 map, 6-cell plots (6x6 plots), start area = plots 2..3 (cells 12..23).
        private static LandPlots NewLand()
        {
            var land = new LandPlots(new GridRect(0, 0, 36, 36), 6, new PlotCoord(2, 2), new PlotCoord(3, 3));
            land.InitializeDefault(null);
            return land;
        }

        [Test]
        public void StartArea_IsOwned_RestIsLocked()
        {
            var land = NewLand();
            Assert.AreEqual(6, land.Columns);
            Assert.AreEqual(4, land.Owned.Count);
            Assert.IsTrue(land.IsCellUnlocked(new GridCoord(12, 12)));
            Assert.IsTrue(land.IsCellUnlocked(new GridCoord(23, 23)));
            Assert.IsFalse(land.IsCellUnlocked(new GridCoord(11, 12)));
            Assert.IsFalse(land.IsCellUnlocked(new GridCoord(24, 20)));
        }

        [Test]
        public void Rings_CountPlotsFromTheStartArea()
        {
            var land = NewLand();
            Assert.AreEqual(0, land.Ring(new PlotCoord(2, 3)));
            Assert.AreEqual(1, land.Ring(new PlotCoord(1, 2)));
            Assert.AreEqual(1, land.Ring(new PlotCoord(4, 4)), "diagonal neighbours are ring 1 too");
            Assert.AreEqual(2, land.Ring(new PlotCoord(0, 5)));
        }

        [Test]
        public void OnlyPlotsNextToOwnedLand_AreForSale()
        {
            var land = NewLand();
            Assert.IsTrue(land.IsForSale(new PlotCoord(1, 2)));
            Assert.IsFalse(land.IsForSale(new PlotCoord(1, 1)), "only shares a corner");
            Assert.IsFalse(land.IsForSale(new PlotCoord(0, 2)), "not adjacent yet");
            Assert.IsFalse(land.IsForSale(new PlotCoord(2, 2)), "already owned");
            Assert.AreEqual(8, land.PlotsForSale().Count());

            PlotCoord bought = default;
            land.PlotBought += p => bought = p;
            Assert.IsTrue(land.Buy(new PlotCoord(1, 2)));
            Assert.AreEqual(new PlotCoord(1, 2), bought);
            Assert.IsTrue(land.IsCellUnlocked(new GridCoord(6, 12)));
            Assert.IsTrue(land.IsForSale(new PlotCoord(0, 2)), "now adjacent");
            Assert.IsFalse(land.Buy(new PlotCoord(5, 5)), "not adjacent");
        }

        [Test]
        public void OldTowns_KeepThePlotsUnderTheirBuildings()
        {
            var land = new LandPlots(new GridRect(0, 0, 36, 36), 6, new PlotCoord(2, 2), new PlotCoord(3, 3));
            land.InitializeDefault(new[] { new GridCoord(30, 30), new GridCoord(31, 30) });
            Assert.IsTrue(land.IsOwned(new PlotCoord(5, 5)));
            Assert.AreEqual(5, land.Owned.Count);
        }

        [Test]
        public void PlotIds_RoundTrip()
        {
            Assert.IsTrue(PlotCoord.TryParse("3_2", out var p));
            Assert.AreEqual(new PlotCoord(3, 2), p);
            Assert.AreEqual("3_2", p.Id);
            Assert.IsFalse(PlotCoord.TryParse("x", out _));
            Assert.IsFalse(PlotCoord.TryParse(null, out _));
        }

        [Test]
        public void Town_RefusesBuildingsOnLockedLand()
        {
            var land = NewLand();
            var town = new Town(new GridRect(0, 0, 36, 36));
            town.SetLandCheck(land.IsCellUnlocked);
            Assert.AreEqual(PlacementResult.Valid, town.CanPlace(new GridCoord(12, 12), new GridCoord(2, 2)));
            Assert.AreEqual(PlacementResult.LockedLand, town.CanPlace(new GridCoord(11, 12), new GridCoord(2, 2)), "half on locked land");
            Assert.AreEqual(PlacementResult.LockedLand, town.TryAdd("a", "cottage", new GridCoord(2, 2), new GridCoord(2, 2), out _));
            land.Buy(new PlotCoord(1, 2));
            Assert.AreEqual(PlacementResult.Valid, town.CanPlace(new GridCoord(11, 12), new GridCoord(2, 2)));
        }

        [Test]
        public void EdgePlots_CanBeSmaller()
        {
            var land = new LandPlots(new GridRect(0, 0, 32, 32), 6, new PlotCoord(2, 2), new PlotCoord(3, 3));
            Assert.AreEqual(6, land.Columns);
            Assert.AreEqual(new GridCoord(2, 2), land.PlotCells(new PlotCoord(5, 5)));
            Assert.AreEqual(new GridCoord(6, 6), land.PlotCells(new PlotCoord(0, 0)));
        }
    }
}
