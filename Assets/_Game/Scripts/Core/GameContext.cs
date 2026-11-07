using System;
using TownBuilder.Domain;

namespace TownBuilder
{
    /// <summary>
    /// Application-wide services, created once by <see cref="GameRoot"/>. Scenes receive what they need from here
    /// instead of reaching for many global singletons.
    /// </summary>
    public sealed class GameContext
    {
        public GameContext(GameContent content, JsonSaveStore saveStore, IClock clock, IAdService ads)
        {
            Content = content ? content : throw new ArgumentNullException(nameof(content));
            SaveStore = saveStore ?? throw new ArgumentNullException(nameof(saveStore));
            Clock = clock ?? throw new ArgumentNullException(nameof(clock));
            Ads = ads ?? new NoAdService();
        }

        public GameContent Content { get; }
        public IAdService Ads { get; }

        /// <summary>Music and sound effects (set by GameRoot; null only in unusual test setups).</summary>
        public GameAudio Audio { get; internal set; }
        public JsonSaveStore SaveStore { get; }
        public IClock Clock { get; }

        /// <summary>The save the player is currently playing. Null until New Game / Continue.</summary>
        public SaveData ActiveSave { get; private set; }

        public bool HasActiveSave => ActiveSave != null;

        public void StartNewGame()
        {
            ActiveSave = NewGameFactory.Create(Clock.UtcNowUnixMs, Content.StartingCoins, Content.StartingGems);
            foreach (var starter in Content.StarterBuildings)
            {
                if (starter.building == null) continue;
                ActiveSave.buildings.Add(new BuildingSaveData
                {
                    instanceId = Guid.NewGuid().ToString("N"),
                    definitionId = starter.building.Id,
                    x = starter.origin.x,
                    y = starter.origin.y,
                });
            }

            SaveActive();
        }

        public LoadResult ContinueGame()
        {
            var result = SaveStore.Load();
            if (result.Succeeded) ActiveSave = result.Data;
            return result;
        }

        public void SaveActive()
        {
            if (ActiveSave == null) return;
            ActiveSave.savedAtUnixMs = Clock.UtcNowUnixMs;
            SaveStore.Save(ActiveSave);
        }
    }
}
