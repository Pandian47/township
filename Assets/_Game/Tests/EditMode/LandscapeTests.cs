using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TownBuilder.Domain;

namespace TownBuilder.Tests
{
    public sealed class LandscapeTests
    {
        private Dictionary<GridCoord, Terrain> _ground;
        private HashSet<GridCoord> _owned;
        private HashSet<GridCoord> _buildings;

        [SetUp]
        public void SetUp()
        {
            _ground = new Dictionary<GridCoord, Terrain>();
            _owned = new HashSet<GridCoord>();
            for (int x = 0; x < 5; x++)
            for (int y = 0; y < 5; y++)
                _owned.Add(new GridCoord(x, y));
            _buildings = new HashSet<GridCoord> { new GridCoord(2, 2) };
        }

        private LandscapeEdit NewEdit() => new LandscapeEdit(
            c => _ground.TryGetValue(c, out var t) ? t : Terrain.Grass,
            c => _owned.Contains(c),
            c => _buildings.Contains(c));

        [Test]
        public void Paint_OwnLandOnly_AndNoWaterOrRoadUnderBuildings()
        {
            var edit = NewEdit();
            Assert.AreEqual(PaintResult.Painted, edit.TryPaint(new GridCoord(0, 0), Terrain.Sand));
            Assert.AreEqual(PaintResult.NotYourLand, edit.TryPaint(new GridCoord(9, 9), Terrain.Sand));
            Assert.AreEqual(PaintResult.UnderBuilding, edit.TryPaint(new GridCoord(2, 2), Terrain.Water));
            Assert.AreEqual(PaintResult.UnderBuilding, edit.TryPaint(new GridCoord(2, 2), Terrain.Road));
            Assert.AreEqual(PaintResult.Painted, edit.TryPaint(new GridCoord(2, 2), Terrain.Pavement), "plain ground is fine under a building");
            Assert.AreEqual(PaintResult.NoChange, edit.TryPaint(new GridCoord(1, 1), Terrain.Grass));
        }

        [Test]
        public void Edit_RemembersTheOriginal_ForUndo_AndForgetsCellsPaintedBack()
        {
            _ground[new GridCoord(1, 0)] = Terrain.Water;
            var edit = NewEdit();
            var cell = new GridCoord(1, 0);
            edit.TryPaint(cell, Terrain.Grass);
            _ground[cell] = Terrain.Grass; // the controller applies each stroke
            edit.TryPaint(cell, Terrain.Road);
            _ground[cell] = Terrain.Road;
            Assert.AreEqual(Terrain.Water, edit.Before[cell], "the first value is kept");
            Assert.AreEqual(Terrain.Road, edit.After[cell]);

            edit.TryPaint(cell, Terrain.Water);
            Assert.IsFalse(edit.After.ContainsKey(cell), "back to how it was: nothing to save");
            Assert.AreEqual(0, edit.ChangedCount);
        }

        [Test]
        public void Map_CommitsAnEdit()
        {
            var map = new LandscapeMap();
            var edit = NewEdit();
            edit.TryPaint(new GridCoord(0, 0), Terrain.Soil);
            edit.TryPaint(new GridCoord(0, 1), Terrain.Water);
            map.Commit(edit);
            Assert.AreEqual(2, map.Cells.Count);
            Assert.AreEqual(Terrain.Water, map.Cells[new GridCoord(0, 1)]);
            Assert.IsTrue(TerrainRules.IsBlocking(Terrain.Water));
            Assert.IsFalse(TerrainRules.IsBlocking(Terrain.Pavement));
        }

        [Test]
        public void GridLine_HasNoGaps()
        {
            var line = GridLine.Between(new GridCoord(0, 0), new GridCoord(4, 2)).ToList();
            Assert.AreEqual(new GridCoord(0, 0), line.First());
            Assert.AreEqual(new GridCoord(4, 2), line.Last());
            for (int i = 1; i < line.Count; i++)
                Assert.LessOrEqual(System.Math.Max(System.Math.Abs(line[i].X - line[i - 1].X), System.Math.Abs(line[i].Y - line[i - 1].Y)), 1);
        }
    }
}
