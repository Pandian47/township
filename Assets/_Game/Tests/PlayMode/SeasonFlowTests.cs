using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TownBuilder.Domain;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace TownBuilder.Tests
{
    /// <summary>Milestone 8 step 4: the town follows the season of the phone's date.</summary>
    public sealed class SeasonFlowTests
    {
        private string _saveDir;

        [SetUp]
        public void SetUp()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            _saveDir = Path.Combine(Path.GetTempPath(), "TownBuilderSeasonTests", Guid.NewGuid().ToString("N"));
            GameRoot.SaveDirectoryOverride = _saveDir;
            // Mid-October, noon UTC: autumn in every time zone.
            GameRoot.ClockOverride = new ManualClock(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds());
        }

        [TearDown]
        public void TearDown()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            GameRoot.SaveDirectoryOverride = null;
            GameRoot.ClockOverride = null;
            if (Directory.Exists(_saveDir)) Directory.Delete(_saveDir, true);
        }

        [UnityTest]
        public IEnumerator Autumn_GoldenGrass_FallingLeaves_PumpkinsForSale_ThenWinter()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            var seasons = scene.Seasons;
            Assert.AreEqual(Season.Autumn, seasons.Current);

            var ground = GameObject.Find("GroundTilemap").GetComponent<Tilemap>();
            var autumnGrass = seasons.GrassFor(Season.Autumn);
            Assert.IsNotNull(autumnGrass);
            Assert.AreEqual(autumnGrass, ground.GetTile(new Vector3Int(5, 5, 0)), "grass swapped for the autumn grass");
            Assert.Greater(seasons.FallingVisible, 0, "leaves are falling");

            content.TryGetBuilding("pumpkin_pile", out var pumpkins);
            content.TryGetBuilding("snowman", out var snowman);
            Assert.IsTrue(pumpkins.IsSeasonal && pumpkins.Season == Season.Autumn);
            Assert.IsTrue(seasons.IsListedInShop(pumpkins));
            Assert.IsFalse(seasons.IsListedInShop(snowman), "the snowman waits for winter");
            content.TryGetBuilding("tree", out var tree);
            Assert.IsTrue(seasons.IsListedInShop(tree), "normal decorations always");

            seasons.DevNextSeason(content);
            Assert.AreEqual(Season.Winter, seasons.Current);
            Assert.AreEqual(seasons.GrassFor(Season.Winter), ground.GetTile(new Vector3Int(5, 5, 0)), "snowy grass");
            Assert.IsTrue(seasons.IsListedInShop(snowman));
            Assert.IsFalse(seasons.IsListedInShop(pumpkins));
            Assert.AreEqual(4, content.Buildings.Count(b => b.IsSeasonal), "one seasonal decoration per season");
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
