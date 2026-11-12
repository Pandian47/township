using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TownBuilder.Tests
{
    /// <summary>v0.37.0: the Feed Mill comes alive (idle Z letters; working grain, dust, workers, rumble).</summary>
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
        public IEnumerator FeedMill_IdleSleeps_WorkingComesAlive()
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
            Assert.IsTrue(GameRoot.Current.Context.Content.TryGetBuilding("feed_mill", out var mill));
            var spec = mill.Working;
            Assert.IsTrue(spec.enabled, "the Feed Mill has a working animation");
            foreach (var p in new[] { spec.sleepPoint, spec.streamPoint, spec.dustPoint, spec.walkFrom, spec.walkTo, spec.workerSpot })
                Assert.IsTrue(p.x > 0f && p.x < 1f && p.y > 0f && p.y < 1f, $"point {p} is on the sprite");

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
            Assert.IsTrue(life.WorkersVisible(view.InstanceId), "workers appear");
            Assert.Greater(life.ParticleCount, 3, "grain is falling");
            Assert.AreNotEqual(Vector3.one, view.transform.localScale, "the mill rumbles");

            life.SetState(view, FactoryLife.State.Idle);
            Assert.AreEqual(Vector3.one, view.transform.localScale, "back to its normal size");
            Assert.IsFalse(life.WorkersVisible(view.InstanceId));
        }
    }
}
