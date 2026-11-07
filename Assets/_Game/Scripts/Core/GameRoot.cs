using System.IO;
using TownBuilder.Domain;
using UnityEngine;

namespace TownBuilder
{
    /// <summary>
    /// The single persistent object of the game (DontDestroyOnLoad). It owns the <see cref="GameContext"/>.
    /// This is the only global access point; everything else gets its dependencies passed in explicitly.
    /// Every scene can create it on demand, so MainMenu and Town also run when opened directly in the Editor.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameRoot : MonoBehaviour
    {
        public const string SaveFolderName = "saves";
        private const int DefaultTargetFrameRate = 60;

        public static GameRoot Current { get; private set; }

        /// <summary>Tests set this to an isolated folder so they never touch the developer's real save.</summary>
        public static string SaveDirectoryOverride { get; set; }

        public GameContext Context { get; private set; }

        public static GameRoot GetOrCreate(GameContent content)
        {
            if (Current != null) return Current;
            if (content == null)
            {
                Debug.LogError("[GameRoot] No GameContent assigned. Run 'TownBuilder > Setup > Run Full Setup'.");
                return null;
            }

            var go = new GameObject("[GameRoot]");
            DontDestroyOnLoad(go);
            var root = go.AddComponent<GameRoot>();
            root.Initialize(content);
            return root;
        }

        private void Initialize(GameContent content)
        {
            foreach (var problem in content.Validate())
                Debug.LogWarning($"[GameContent] {problem}", content);

            var saveDirectory = !string.IsNullOrEmpty(SaveDirectoryOverride)
                ? SaveDirectoryOverride
                : Path.Combine(Application.persistentDataPath, SaveFolderName);
            var store = new JsonSaveStore(saveDirectory);
            Context = new GameContext(content, store, new SystemClock());
            Current = this;

            Application.targetFrameRate = DefaultTargetFrameRate;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }
    }
}
