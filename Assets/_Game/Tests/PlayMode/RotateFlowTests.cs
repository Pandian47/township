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
    /// <summary>v0.40.0: long press picks a building up; Rotate turns it (mirrored picture); the facing is saved.</summary>
    public sealed class RotateFlowTests
    {
        private string _saveDir;

        [SetUp]
        public void SetUp()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            _saveDir = Path.Combine(Path.GetTempPath(), "TownBuilderRotateTests", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator LongPress_Rotate_Mirrors_AndIsSaved()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            var placement = scene.Placement;
            var views = Object.FindAnyObjectByType<TownBuildingController>();

            // Place a Feed Mill (2x2) on free land to work with.
            Assert.IsTrue(content.TryGetBuilding("feed_mill", out var millDef));
            var origin = FreeOrigin(scene.Town, millDef.FootprintSize);
            Assert.AreEqual(PlacementResult.Valid, scene.Town.TryAdd("mill_test", "feed_mill", origin, millDef.FootprintSize, out var mill));
            Assert.IsTrue(views.TryGetView("mill_test", out var view));
            Assert.IsFalse(view.Flipped);
            var chimneyLike = new Vector2(0.8f, 0.5f);
            float normalX = view.PointLocal(chimneyLike).x;

            // Long press picks it up straight into moving.
            var screen = Camera.main.WorldToScreenPoint(view.transform.position + new Vector3(0f, 0.3f, 0f));
            placement.LongPressAt(screen);
            Assert.AreEqual(PlacementSystem.Mode.Moving, placement.CurrentMode, "long press = move");
            Assert.IsTrue(placement.CanRotate);

            // Rotate while moving, then cancel: back to the old facing.
            placement.Rotate();
            Assert.IsTrue(placement.PreviewFlipped);
            Assert.IsTrue(view.Flipped, "the picked-up building turns at once");
            placement.Cancel();
            Assert.IsFalse(view.Flipped, "cancel restores the facing");
            Assert.IsFalse(mill.Flipped);

            // Rotate while moving, then confirm: the building keeps the new facing.
            placement.LongPressAt(screen);
            placement.Rotate();
            Assert.IsTrue(placement.Confirm());
            Assert.IsTrue(mill.Flipped);
            Assert.IsTrue(view.Flipped);
            Assert.IsTrue(view.SpriteRenderer.flipX, "no right-facing picture: the normal one is mirrored");
            Assert.AreEqual(-normalX, view.PointLocal(chimneyLike).x, 1e-4f, "effects follow the mirrored picture");

            // Rotate from the selection bar: turns back where it stands.
            placement.SelectBuilding("mill_test");
            Assert.AreEqual(PlacementResult.Valid, placement.Rotate());
            Assert.IsFalse(mill.Flipped);
            Assert.IsFalse(view.SpriteRenderer.flipX);
            placement.Rotate(); // flipped again, to check the save

            // Restart: still turned.
            scene.SaveNow();
            Object.DestroyImmediate(GameRoot.Current.gameObject);
            yield return null;
            SceneManager.LoadScene(SceneNames.MainMenu);
            float end = Time.realtimeSinceStartup + 5f;
            while (GameRoot.Current == null && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(GameRoot.Current.Context.ContinueGame().Succeeded);
            yield return LoadTown();
            scene = Object.FindAnyObjectByType<TownSceneController>();
            Assert.IsTrue(scene.Town.TryGet("mill_test", out var loaded));
            Assert.IsTrue(loaded.Flipped, "the facing is saved");
            Assert.IsTrue(Object.FindAnyObjectByType<TownBuildingController>().TryGetView("mill_test", out var loadedView));
            Assert.IsTrue(loadedView.Flipped && loadedView.SpriteRenderer.flipX);
        }

        private static GridCoord FreeOrigin(Town town, GridCoord size)
        {
            for (int x = 12; x <= 22; x++)
            for (int y = 12; y <= 22; y++)
                if (town.CanPlace(new GridCoord(x, y), size) == PlacementResult.Valid) return new GridCoord(x, y);
            Assert.Fail("no free spot in the start area");
            return default;
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
