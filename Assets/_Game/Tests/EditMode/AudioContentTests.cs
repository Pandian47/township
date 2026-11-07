using System;
using NUnit.Framework;
using UnityEditor;

namespace TownBuilder.Tests
{
    /// <summary>Content checks on the real assets (catch a missing clip before it ships).</summary>
    public sealed class AudioContentTests
    {
        private const string LibraryPath = "Assets/_Game/ScriptableObjects/SoundLibrary.asset";
        private const string ContentPath = "Assets/_Game/ScriptableObjects/GameContent.asset";

        [Test]
        public void EverySoundId_HasAClip()
        {
            var library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
            Assert.NotNull(library, "Run TownBuilder > Setup > Run Full Setup");
            foreach (SoundId id in Enum.GetValues(typeof(SoundId)))
                Assert.IsTrue(library.TryGet(id, out var entry) && entry.clip != null, $"No clip for {id}");
            Assert.NotNull(library.Music, "Music loop missing");
            Assert.Greater(library.Music.length, 5f, "Music loop is suspiciously short");
        }

        [Test]
        public void GameContent_ReferencesSoundLibrary()
        {
            var content = AssetDatabase.LoadAssetAtPath<GameContent>(ContentPath);
            Assert.NotNull(content.SoundLibrary);
        }

        [Test]
        public void GameContent_IsValid()
        {
            var content = AssetDatabase.LoadAssetAtPath<GameContent>(ContentPath);
            CollectionAssert.IsEmpty(content.Validate());
        }
    }
}
