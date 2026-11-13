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

        /// <summary>Tests set this to a <see cref="ManualClock"/> to fast-forward crop and production timers.</summary>
        public static IClock ClockOverride { get; set; }

        /// <summary>Tests only: the ad service to use instead of the one AdsConfig picks.</summary>
        public static IAdService AdServiceOverride { get; set; }

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
            Context = new GameContext(content, store, ClockOverride ?? new SystemClock(), AdServiceOverride ?? CreateAdService(content.AdsConfig));
            Current = this;

            var audio = gameObject.AddComponent<GameAudio>();
            audio.Initialize(content.SoundLibrary);
            Context.Audio = audio;

            Application.targetFrameRate = DefaultTargetFrameRate;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
        }

        /// <summary>Real AdMob on Android/iOS devices, a simulated service in the Editor, nothing when disabled.</summary>
        private IAdService CreateAdService(AdsConfig config)
        {
            if (config == null || !config.AdsEnabled) return new NoAdService();
            if (Application.isEditor) return new SimulatedAdService();
            if (Application.platform != RuntimePlatform.Android && Application.platform != RuntimePlatform.IPhonePlayer)
                return new NoAdService();

            var admob = new AdMobAdService(config, gameObject.AddComponent<CoroutineRunner>());
            admob.Initialize();
            return admob;
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }
    }
}
