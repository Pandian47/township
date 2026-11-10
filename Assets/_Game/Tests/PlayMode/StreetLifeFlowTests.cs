using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TownBuilder.Domain;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TownBuilder.Tests
{
    /// <summary>Milestone 6 step 3: townspeople and cars on the roads.</summary>
    public sealed class StreetLifeFlowTests
    {
        private string _saveDir;

        [SetUp]
        public void SetUp()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            _saveDir = Path.Combine(Path.GetTempPath(), "TownBuilderStreetTests", System.Guid.NewGuid().ToString("N"));
            GameRoot.SaveDirectoryOverride = _saveDir;
        }

        [TearDown]
        public void TearDown()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            GameRoot.SaveDirectoryOverride = null;
            if (Directory.Exists(_saveDir)) Directory.Delete(_saveDir, true);
        }

        [UnityTest]
        public IEnumerator PeopleAndCars_MoveAlongTheRoads()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var street = scene.StreetLife;
            Assert.NotNull(street);
            Assert.Greater(street.RoadCellCount, 10, "road graph built from the road tilemap");
            Assert.Greater(street.People, 0);
            Assert.Greater(street.Cars, 0);

            var before = street.Positions().ToList();
            yield return new WaitForSecondsRealtime(1.0f);
            var after = street.Positions().ToList();
            Assert.IsTrue(before.Zip(after, (a, b) => Vector3.Distance(a, b)).Any(d => d > 0.05f), "someone moved");

            // Everyone stays on (or right beside) a road cell.
            var grid = Object.FindAnyObjectByType<IsoGrid>();
            foreach (var p in after)
            {
                var cell = grid.WorldToCell(p);
                bool nearRoad = street.IsRoad(cell) || street.Neighbours(cell).Count > 0 ||
                                new[] { new GridCoord(cell.X + 1, cell.Y), new GridCoord(cell.X - 1, cell.Y), new GridCoord(cell.X, cell.Y + 1), new GridCoord(cell.X, cell.Y - 1) }
                                    .Any(street.IsRoad);
                Assert.IsTrue(nearRoad, $"walker at {cell} is off the roads");
            }
        }

        [UnityTest]
        public IEnumerator MoreCottages_MorePeople()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            int people = scene.StreetLife.People;
            GameRoot.Current.Context.Content.TryGetBuilding("cottage", out var cottage);
            scene.Wallet.AddCoins(5_000);
            Assert.IsTrue(scene.Placement.BeginPlacement(cottage));
            Assert.IsTrue(scene.Placement.Confirm());
            yield return new WaitForSecondsRealtime(2.3f); // the census runs every 2 s
            Assert.AreEqual(people + 1, scene.StreetLife.People);
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
