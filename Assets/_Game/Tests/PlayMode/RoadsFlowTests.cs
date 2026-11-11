using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TownBuilder.Domain;
using UnityEngine;
using Terrain = TownBuilder.Domain.Terrain;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

namespace TownBuilder.Tests
{
    /// <summary>v0.36.0: three landscape tools, road routes with a flag, joined road tiles, undo, eraser, saved styles.</summary>
    public sealed class RoadsFlowTests
    {
        private string _saveDir;

        [SetUp]
        public void SetUp()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            _saveDir = Path.Combine(Path.GetTempPath(), "TownBuilderRoadsTests", System.Guid.NewGuid().ToString("N"));
            GameRoot.SaveDirectoryOverride = _saveDir;
        }

        [TearDown]
        public void TearDown()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            GameRoot.SaveDirectoryOverride = null;
            if (Directory.Exists(_saveDir)) Directory.Delete(_saveDir, true);
        }

        /// <summary>Three free grass cells in a row along X (own land, nothing built).</summary>
        private static GridCoord FreeRow(TownSceneController scene)
        {
            bool Free(GridCoord c) => scene.Landscape.CurrentTerrain(c) == Terrain.Grass && scene.Town.CanPlace(c, new GridCoord(1, 1)) == PlacementResult.Valid;
            for (int y = 12; y <= 23; y++)
            for (int x = 12; x <= 21; x++)
            {
                var c = new GridCoord(x, y);
                if (Free(c) && Free(new GridCoord(x + 1, y)) && Free(new GridCoord(x + 2, y))) return c;
            }

            Assert.Fail("no free row of three grass cells in the start area");
            return default;
        }

        [UnityTest]
        public IEnumerator ThreeFreeToolCards_FirstInDecorations()
        {
            yield return LoadTown();
            var content = GameRoot.Current.Context.Content;
            var tools = content.Buildings.Where(b => b.IsLandscapeTool).OrderBy(b => b.LandscapeToolOrder).ToList();
            Assert.AreEqual(3, tools.Count);
            CollectionAssert.AreEqual(new[] { LandscapeTool.Roads, LandscapeTool.Paving, LandscapeTool.Ground }, tools.Select(t => t.LandscapeTool).ToArray());
            Assert.IsTrue(tools.All(t => t.CoinCost == 0 && t.Category == BuildingCategory.Decoration));
        }

        [UnityTest]
        public IEnumerator RoadRoute_LaysJoinedRoad_BlocksBuilding_UndoAndSave()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var landscape = scene.Landscape;
            var start = FreeRow(scene);
            var end = new GridCoord(start.X + 2, start.Y);
            var middle = new GridCoord(start.X + 1, start.Y);

            landscape.Begin(LandscapeTool.Roads);
            Assert.AreEqual(LandscapeTool.Roads, landscape.Tool);
            Assert.AreEqual(Terrain.Road, landscape.Selected, "starts on the first road style");
            Assert.IsTrue(scene.Hud.LandscapePanel.IsOpen);

            landscape.Select(Terrain.DirtPath);
            landscape.SetRoute(start, end);
            Assert.IsTrue(landscape.HasRoute);
            Assert.IsTrue(landscape.RouteValid);
            Assert.AreEqual(3, landscape.Route.Count);
            Assert.AreEqual(Terrain.Grass, landscape.CurrentTerrain(middle), "only a preview until Build");

            landscape.Confirm(); // the green button builds the road
            Assert.IsFalse(landscape.HasRoute);
            foreach (var c in new[] { start, middle, end })
            {
                Assert.AreEqual(Terrain.DirtPath, landscape.CurrentTerrain(c));
                Assert.AreEqual(PlacementResult.BlockedTerrain, scene.Town.CanPlace(c, new GridCoord(1, 1)), "can't build on a path");
            }

            // Joined: the middle tile shows the picture with roads on both X sides.
            var roadMap = GameObject.Find("RoadTilemap").GetComponent<Tilemap>();
            var tile = roadMap.GetTile(new Vector3Int(middle.X, middle.Y, 0)) as JoinedRoadTile;
            Assert.IsNotNull(tile, "roads use joined road tiles");
            int mask = RoadJoins.Mask(middle, c => roadMap.GetTile(new Vector3Int(c.X, c.Y, 0)) is JoinedRoadTile);
            Assert.AreEqual(RoadJoins.PlusX | RoadJoins.MinusX, mask & (RoadJoins.PlusX | RoadJoins.MinusX));
            Assert.AreEqual(tile.SpriteFor(mask), roadMap.GetSprite(new Vector3Int(middle.X, middle.Y, 0)));
            Assert.AreNotEqual(tile.SpriteFor(0), tile.SpriteFor(mask), "a joined tile looks different from a lone one");

            // A road across the Town Hall can't be built.
            var hall = scene.Town.Buildings.First(b => b.DefinitionId == "town_hall").Origin;
            landscape.SetRoute(hall, new GridCoord(hall.X + 1, hall.Y));
            Assert.IsFalse(landscape.RouteValid);
            Assert.IsFalse(landscape.LayRoute());
            landscape.Back(); // the red button removes the pending road, the session goes on
            Assert.IsTrue(landscape.IsPainting);
            Assert.IsFalse(landscape.HasRoute);

            // Eraser and undo.
            landscape.SelectEraser();
            Assert.AreEqual(PaintResult.Painted, landscape.Paint(end));
            Assert.AreEqual(Terrain.Grass, landscape.CurrentTerrain(end));
            landscape.Undo();
            Assert.AreEqual(Terrain.DirtPath, landscape.CurrentTerrain(end), "undo brings the erased tile back");
            landscape.Done();
            Assert.IsFalse(landscape.IsPainting);
            Assert.AreEqual(Terrain.DirtPath, landscape.Map.Cells[middle]);

            // Restart: the dirt path is still there and still joined.
            scene.SaveNow();
            Object.DestroyImmediate(GameRoot.Current.gameObject);
            yield return null;
            SceneManager.LoadScene(SceneNames.MainMenu);
            float until = Time.realtimeSinceStartup + 5f;
            while (GameRoot.Current == null && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(GameRoot.Current.Context.ContinueGame().Succeeded);
            yield return LoadTown();
            scene = Object.FindAnyObjectByType<TownSceneController>();
            Assert.AreEqual(Terrain.DirtPath, scene.Landscape.CurrentTerrain(middle));
            Assert.AreEqual(PlacementResult.BlockedTerrain, scene.Town.CanPlace(middle, new GridCoord(1, 1)));

            // Undo of a whole road in one step.
            scene.Landscape.Begin(LandscapeTool.Roads);
            scene.Landscape.SetRoute(start, end);
            scene.Landscape.Select(Terrain.Boardwalk);
            Assert.IsTrue(scene.Landscape.LayRoute());
            Assert.AreEqual(Terrain.Boardwalk, scene.Landscape.CurrentTerrain(start));
            scene.Landscape.Undo();
            Assert.AreEqual(Terrain.DirtPath, scene.Landscape.CurrentTerrain(start));
            Assert.AreEqual(Terrain.DirtPath, scene.Landscape.CurrentTerrain(end));
            scene.Landscape.Cancel();
        }

        [UnityTest]
        public IEnumerator Paving_PaintsFreehand_AndStaysBuildable()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var cell = FreeRow(scene);
            scene.Landscape.Begin(LandscapeTool.Paving);
            Assert.AreEqual(Terrain.Pavement, scene.Landscape.Selected);
            Assert.IsFalse(scene.Landscape.DrawsRoutes, "paving is painted with a finger, not with a flag");
            scene.Landscape.Select(Terrain.CheckerTiles);
            Assert.AreEqual(PaintResult.Painted, scene.Landscape.Paint(cell));
            Assert.AreEqual(Terrain.CheckerTiles, scene.Landscape.CurrentTerrain(cell));
            Assert.AreEqual(PlacementResult.Valid, scene.Town.CanPlace(cell, new GridCoord(1, 1)), "you can build on paving");
            scene.Landscape.Cancel();
            Assert.AreEqual(Terrain.Grass, scene.Landscape.CurrentTerrain(cell));
        }

        private static IEnumerator LoadTown()
        {
            SceneManager.LoadScene(SceneNames.Town);
            float end = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < end)
            {
                var c = Object.FindAnyObjectByType<TownSceneController>();
                if (c != null && c.IsReady) yield break;
                yield return null;
            }

            Assert.Fail("Town scene did not initialise");
        }
    }
}
