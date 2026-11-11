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

        // ------------------------------------------------------------------ v0.36.0: tools, routes, joins, undo

        [Test]
        public void EveryRoadStyle_BlocksBuilding_PavingAndGroundDont()
        {
            foreach (var road in LandscapePalette.For(LandscapeTool.Roads))
            {
                Assert.IsTrue(TerrainRules.IsRoad(road), road.ToString());
                Assert.IsTrue(TerrainRules.IsBlocking(road), road.ToString());
            }

            foreach (var paving in LandscapePalette.For(LandscapeTool.Paving))
            {
                Assert.IsTrue(TerrainRules.IsPaving(paving), paving.ToString());
                Assert.IsFalse(TerrainRules.IsBlocking(paving), paving.ToString());
            }

            Assert.Contains(Terrain.Water, (System.Collections.ICollection)LandscapePalette.For(LandscapeTool.Ground));
            Assert.IsFalse(TerrainRules.IsBlocking(Terrain.Meadow));
            Assert.AreEqual(Terrain.Road, LandscapePalette.For(LandscapeTool.Roads)[0], "the first road style is the town's own stone road");
            Assert.AreEqual(Terrain.Grass, LandscapePalette.For(LandscapeTool.Ground)[0]);

            var all = new HashSet<Terrain>(LandscapePalette.For(LandscapeTool.Roads)
                .Concat(LandscapePalette.For(LandscapeTool.Paving)).Concat(LandscapePalette.For(LandscapeTool.Ground)));
            Assert.AreEqual(System.Enum.GetValues(typeof(Terrain)).Length, all.Count, "every style is in exactly one tool");
        }

        [Test]
        public void RoadRoute_StraightOrOneCorner_Connected()
        {
            var straight = RoadRoute.Path(new GridCoord(1, 1), new GridCoord(1, 4), true);
            CollectionAssert.AreEqual(new[] { new GridCoord(1, 1), new GridCoord(1, 2), new GridCoord(1, 3), new GridCoord(1, 4) }, straight);

            var xFirst = RoadRoute.Path(new GridCoord(0, 0), new GridCoord(2, -2), true);
            CollectionAssert.AreEqual(new[] { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0), new GridCoord(2, -1), new GridCoord(2, -2) }, xFirst);
            var yFirst = RoadRoute.Path(new GridCoord(0, 0), new GridCoord(2, -2), false);
            Assert.AreEqual(new GridCoord(0, -2), yFirst[2], "the other corner");
            Assert.AreEqual(new GridCoord(2, -2), yFirst.Last());

            Assert.AreEqual(1, RoadRoute.Path(new GridCoord(3, 3), new GridCoord(3, 3), true).Count, "a tap is a one-tile road");
            Assert.IsTrue(RoadRoute.PreferXFirst(new GridCoord(0, 0), new GridCoord(5, 1)));
            Assert.IsFalse(RoadRoute.PreferXFirst(new GridCoord(0, 0), new GridCoord(1, 5)));
        }

        [Test]
        public void RoadJoins_MaskHasOneBitPerNeighbouringRoad()
        {
            var roads = new HashSet<GridCoord> { new GridCoord(5, 5), new GridCoord(6, 5), new GridCoord(5, 4) };
            int mask = RoadJoins.Mask(new GridCoord(5, 5), roads.Contains);
            Assert.AreEqual(RoadJoins.PlusX | RoadJoins.MinusY, mask);
            Assert.AreEqual(RoadJoins.MinusX, RoadJoins.Mask(new GridCoord(6, 5), roads.Contains));
            Assert.AreEqual(0, RoadJoins.Mask(new GridCoord(9, 9), roads.Contains));
        }

        [Test]
        public void Check_ChangesNothing_AndMatchesTryPaint()
        {
            var edit = NewEdit();
            Assert.AreEqual(PaintResult.UnderBuilding, edit.Check(new GridCoord(2, 2), Terrain.DirtPath));
            Assert.AreEqual(PaintResult.NotYourLand, edit.Check(new GridCoord(7, 0), Terrain.Boardwalk));
            Assert.AreEqual(PaintResult.Painted, edit.Check(new GridCoord(0, 0), Terrain.BrickLane));
            Assert.AreEqual(0, edit.ChangedCount);
            Assert.IsFalse(edit.CanUndo);
        }

        [Test]
        public void Undo_TakesBackOneStrokeAtATime()
        {
            var edit = NewEdit();
            void Paint(int x, int y, Terrain t)
            {
                var c = new GridCoord(x, y);
                if (edit.TryPaint(c, t) == PaintResult.Painted) _ground[c] = t;
            }

            edit.BeginStroke(); // a road of three tiles
            Paint(0, 0, Terrain.Road);
            Paint(1, 0, Terrain.Road);
            Paint(2, 0, Terrain.Road);
            edit.EndStroke();
            Paint(1, 0, Terrain.Sand); // a single tap over the middle of it
            Assert.AreEqual(3, edit.ChangedCount);

            var restored = edit.UndoLastStroke();
            Assert.AreEqual(1, restored.Count);
            Assert.AreEqual(Terrain.Road, restored[0].Value, "the tap is undone: road again");
            foreach (var p in restored) _ground[p.Key] = p.Value;
            Assert.AreEqual(Terrain.Road, edit.After[new GridCoord(1, 0)]);

            restored = edit.UndoLastStroke();
            Assert.AreEqual(3, restored.Count, "the whole road at once");
            Assert.IsTrue(restored.All(p => p.Value == Terrain.Grass));
            Assert.AreEqual(0, edit.ChangedCount, "nothing left to save");
            Assert.IsFalse(edit.CanUndo);
            Assert.AreEqual(0, edit.UndoLastStroke().Count);
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
