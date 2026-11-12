using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TownBuilder.Tests
{
    /// <summary>Production buildings come alive: idle Z letters; working streams and dust (v0.39.1: no workers, no rumble).</summary>
    public sealed class FactoryLifeTests
    {
        private string _saveDir;

        [SetUp]
        public void SetUp()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            _saveDir = Path.Combine(Path.GetTempPath(), "TownBuilderFactoryLifeTests", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator Factory_IdleSleeps_WorkingComesAlive()
        {
            SceneManager.LoadScene(SceneNames.Town);
            float end = Time.realtimeSinceStartup + 5f;
            TownSceneController scene = null;
            while (Time.realtimeSinceStartup < end && (scene == null || !scene.IsReady))
            {
                scene = Object.FindAnyObjectByType<TownSceneController>();
                yield return null;
            }

            Assert.IsTrue(scene != null && scene.IsReady, "town loads");
            var content = GameRoot.Current.Context.Content;
            // Every production building has a working animation with its points on its picture.
            foreach (var id in new[] { "feed_mill", "bakery", "sugar_mill", "creamery", "juice_press", "chicken_coop", "dairy_shed" })
            {
                Assert.IsTrue(content.TryGetBuilding(id, out var def), id);
                Assert.IsTrue(def.Working.enabled, $"{id} has a working animation");
                foreach (var p in new[] { def.Working.sleepPoint, def.Working.streamPoint, def.Working.dustPoint, def.Working.walkFrom, def.Working.walkTo, def.Working.workerSpot })
                    if (WorkingAnimation.IsSet(p)) Assert.IsTrue(p.x > 0f && p.x < 1f && p.y > 0f && p.y < 1f, $"{id}: point {p} is on the sprite");
            }

            // The Feed Mill's tower has no chimney (no smoke), but workers and grain.
            content.TryGetBuilding("feed_mill", out var feedMill);
            Assert.IsFalse(feedMill.HasChimney);
            Assert.IsTrue(WorkingAnimation.IsSet(feedMill.Working.streamPoint));

            // v0.39.1: no building has workers or a bounce.
            foreach (var id in new[] { "feed_mill", "bakery", "sugar_mill", "creamery", "juice_press", "chicken_coop", "dairy_shed" })
            {
                content.TryGetBuilding(id, out var def);
                Assert.IsFalse(WorkingAnimation.IsSet(def.Working.walkFrom) || WorkingAnimation.IsSet(def.Working.workerSpot), $"{id}: no workers");
                Assert.AreEqual(0f, def.Working.rumble, $"{id}: no bounce");
            }

            // The Sugar Mill has a stream and splashes.
            Assert.IsTrue(content.TryGetBuilding("sugar_mill", out var mill));

            var life = Object.FindAnyObjectByType<FactoryLife>();
            Assert.IsNotNull(life, "the town has the FactoryLife effect");
            var view = Object.FindAnyObjectByType<TownBuildingController>().CreatePreview(mill);

            life.SetState(view, FactoryLife.State.Idle);
            // Idle: a burst of three Z letters within the first few seconds, then a rest of 5-10 s.
            int most = 0;
            for (int i = 0; i < 24; i++)
            {
                life.Step(0.25f);
                most = Mathf.Max(most, life.ParticleCount);
            }

            Assert.AreEqual(3, most, "idle: a burst of three sleepy Z letters");
            bool quiet = false; // the burst fades well before the next one (at least 5 s after its last Z)
            for (int i = 0; i < 20; i++)
            {
                life.Step(0.25f);
                quiet |= life.ParticleCount == 0;
            }

            Assert.IsTrue(quiet, "then a quiet gap");
            Assert.IsFalse(life.WorkersVisible(view.InstanceId), "nobody works while idle");

            life.SetState(view, FactoryLife.State.Working);
            for (int i = 0; i < 10; i++) life.Step(0.05f);
            Assert.AreEqual(FactoryLife.State.Working, life.StateOf(view.InstanceId));
            Assert.IsFalse(life.WorkersVisible(view.InstanceId), "no workers");
            Assert.Greater(life.ParticleCount, 3, "juice is pouring");
            Assert.AreEqual(Vector3.one, view.transform.localScale, "no bounce");

            life.SetState(view, FactoryLife.State.Idle);
            Assert.AreEqual(Vector3.one, view.transform.localScale, "back to its normal size");
            Assert.IsFalse(life.WorkersVisible(view.InstanceId));
        }
    }
}
