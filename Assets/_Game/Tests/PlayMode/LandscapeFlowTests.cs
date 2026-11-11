using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TownBuilder.Domain;
using UnityEngine;
using Terrain = TownBuilder.Domain.Terrain;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TownBuilder.Tests
{
    /// <summary>Landscape painting: paint ground on own land, water/road block building, Cancel undoes, saved.</summary>
    public sealed class LandscapeFlowTests
    {
        private string _saveDir;

        [SetUp]
        public void SetUp()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            _saveDir = Path.Combine(Path.GetTempPath(), "TownBuilderLandscapeTests", System.Guid.NewGuid().ToString("N"));
            GameRoot.SaveDirectoryOverride = _saveDir;
        }

        [TearDown]
        public void TearDown()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            GameRoot.SaveDirectoryOverride = null;
            if (Directory.Exists(_saveDir)) Directory.Delete(_saveDir, true);
        }

        private static GridCoord FreeGrassCell(TownSceneController scene)
        {
            for (int x = 12; x <= 23; x++)
            for (int y = 12; y <= 23; y++)
            {
                var c = new GridCoord(x, y);
                if (scene.Landscape.CurrentTerrain(c) == Terrain.Grass && scene.Town.CanPlace(c, new GridCoord(1, 1)) == PlacementResult.Valid) return c;
            }

            Assert.Fail("no free grass cell in the start area");
            return default;
        }

        [UnityTest]
        public IEnumerator LandscapeCard_PaintsWater_BlocksBuilding_IsSaved()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            var tool = content.Buildings.First(b => b.IsLandscapeTool);
            Assert.AreEqual(BuildingCategory.Decoration, tool.Category);
            Assert.AreEqual(0, tool.CoinCost, "free");
            Assert.IsFalse(scene.Placement.BeginPlacement(tool), "a tool, never placed as a building");

            var cell = FreeGrassCell(scene);
            scene.Landscape.Begin();
            Assert.IsTrue(scene.Landscape.IsPainting);
            Assert.IsTrue(scene.Hud.LandscapePanel.IsOpen, "paint bar shows");
            scene.Landscape.Select(Terrain.Water);
            Assert.AreEqual(PaintResult.Painted, scene.Landscape.Paint(cell));
            Assert.AreEqual(Terrain.Water, scene.Landscape.CurrentTerrain(cell));
            Assert.AreEqual(PlacementResult.BlockedTerrain, scene.Town.CanPlace(cell, new GridCoord(1, 1)), "can't build on water");

            var hall = scene.Town.Buildings.First(b => b.DefinitionId == "town_hall").Origin;
            Assert.AreEqual(PaintResult.UnderBuilding, scene.Landscape.Paint(hall));
            Assert.AreEqual(PaintResult.NotYourLand, scene.Landscape.Paint(new GridCoord(1, 1)), "outside the start area");

            scene.Landscape.Done();
            Assert.IsFalse(scene.Landscape.IsPainting);
            Assert.IsFalse(scene.Hud.LandscapePanel.IsOpen);
            Assert.AreEqual(Terrain.Water, scene.Landscape.Map.Cells[cell]);

            // Restart: still water, still not buildable.
            scene.SaveNow();
            Object.DestroyImmediate(GameRoot.Current.gameObject);
            yield return null;
            SceneManager.LoadScene(SceneNames.MainMenu);
            float end = Time.realtimeSinceStartup + 5f;
            while (GameRoot.Current == null && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(GameRoot.Current.Context.ContinueGame().Succeeded);
            yield return LoadTown();
            scene = Object.FindAnyObjectByType<TownSceneController>();
            Assert.AreEqual(Terrain.Water, scene.Landscape.CurrentTerrain(cell));
            Assert.AreEqual(PlacementResult.BlockedTerrain, scene.Town.CanPlace(cell, new GridCoord(1, 1)));

            // Painting grass back makes it buildable again.
            scene.Landscape.Begin();
            scene.Landscape.Select(Terrain.Grass);
            scene.Landscape.Paint(cell);
            scene.Landscape.Done();
            Assert.AreEqual(PlacementResult.Valid, scene.Town.CanPlace(cell, new GridCoord(1, 1)));
        }

        [UnityTest]
        public IEnumerator Cancel_UndoesTheWholeSession()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var cell = FreeGrassCell(scene);
            scene.Landscape.Begin();
            scene.Landscape.Select(Terrain.Road);
            scene.Landscape.Paint(cell);
            Assert.AreEqual(Terrain.Road, scene.Landscape.CurrentTerrain(cell));
            scene.Landscape.Cancel();
            Assert.AreEqual(Terrain.Grass, scene.Landscape.CurrentTerrain(cell));
            Assert.AreEqual(PlacementResult.Valid, scene.Town.CanPlace(cell, new GridCoord(1, 1)));
            Assert.AreEqual(0, scene.Landscape.Map.Cells.Count, "nothing saved");
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
