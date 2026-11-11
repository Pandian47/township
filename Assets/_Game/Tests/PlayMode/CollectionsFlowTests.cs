using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TownBuilder.Tests
{
    /// <summary>Milestone 8 step 3: collections end to end.</summary>
    public sealed class CollectionsFlowTests
    {
        private string _saveDir;

        [SetUp]
        public void SetUp()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            _saveDir = Path.Combine(Path.GetTempPath(), "TownBuilderCollectionTests", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator ThreeCollections_FindAll_Complete_PaysReward_AndIsSaved()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            Assert.AreEqual(3, content.Collections.Count);
            Assert.IsTrue(content.Collections.All(c => c.Items.Count == 5 && c.Items.All(i => i.icon != null)), "5 items with icons each");

            var panel = Object.FindAnyObjectByType<CollectionsPanel>();
            Assert.IsNotNull(panel);
            GameObject.Find("CollectionsButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.IsTrue(panel.IsOpen);

            // DEV finds one missing item at a time: five finds complete the first set.
            var first = content.Collections[0];
            for (int i = 0; i < 5; i++) Assert.IsNotNull(scene.Collections.DevFindItem());
            Assert.IsTrue(scene.Collections.Book.CanComplete(first.Id));
            Assert.AreEqual(1, scene.Collections.Book.CompletableCount);

            long coins = scene.Wallet.Coins, gems = scene.Wallet.Gems;
            Assert.IsTrue(scene.Collections.Complete(first.Id));
            Assert.AreEqual(coins + first.RewardCoins, scene.Wallet.Coins);
            Assert.AreEqual(gems + first.RewardGems, scene.Wallet.Gems);
            Assert.AreEqual(0, scene.Collections.Book.DistinctOwned(first.Id), "one of each used");
            Assert.IsFalse(scene.Collections.Complete(first.Id));

            scene.Collections.DevFindItem(); // the first set is empty again, so it gets the first item
            scene.SaveNow();
            Object.DestroyImmediate(GameRoot.Current.gameObject);
            yield return null;
            SceneManager.LoadScene(SceneNames.MainMenu);
            float end = Time.realtimeSinceStartup + 5f;
            while (GameRoot.Current == null && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(GameRoot.Current.Context.ContinueGame().Succeeded);
            yield return LoadTown();
            scene = Object.FindAnyObjectByType<TownSceneController>();
            Assert.AreEqual(1, scene.Collections.Book.Completions(first.Id));
            Assert.AreEqual(1, scene.Collections.Book.DistinctOwned(first.Id), "found item kept");
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
