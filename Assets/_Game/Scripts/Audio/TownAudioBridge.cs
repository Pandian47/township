using System;
using TownBuilder.Domain;

namespace TownBuilder
{
    /// <summary>
    /// The one place that decides which sound plays for which Town event. Gameplay classes only raise semantic events
    /// (Planted, Harvested, Collected...), so sounds can be changed or muted here without touching gameplay code.
    /// </summary>
    public sealed class TownAudioBridge : IDisposable
    {
        private readonly GameAudio _audio;
        private readonly Action _dispose;

        public TownAudioBridge(GameAudio audio, Town town, PlacementSystem placement, FarmingController farming,
            ProductionController production, ConstructionController construction, StorageService storage,
            OrderService orders, ProgressionService progression)
        {
            _audio = audio;

            Action<PlacedBuilding> placed = _ => Play(SoundId.Place);
            Action<PlacedBuilding> moved = _ => Play(SoundId.Place);
            Action<PlacedBuilding> removed = _ => Play(SoundId.Remove);
            Action<string> planted = _ => Play(SoundId.Plant);
            Action<string> harvested = _ => Play(SoundId.Harvest);
            Action<string> queued = _ => Play(SoundId.Plant);
            Action<string> collected = _ => Play(SoundId.Collect);
            Action<string> built = _ => Play(SoundId.BuildComplete);
            Action<string> sold = _ => Play(SoundId.Coins);
            Action expanded = () => Play(SoundId.BuildComplete);
            Action<int> delivered = _ => Play(SoundId.Coins);
            Action<LevelUpInfo> leveledUp = _ => Play(SoundId.LevelUp);
            Action failed = () => Play(SoundId.Error);

            placement.BuildingPlaced += placed;
            town.BuildingMoved += moved;
            town.BuildingRemoved += removed;
            farming.Planted += planted;
            farming.Harvested += harvested;
            farming.Failed += failed;
            production.Queued += queued;
            production.Collected += collected;
            production.Failed += failed;
            construction.Completed += built;
            construction.Failed += failed;
            storage.Sold += sold;
            storage.Expanded += expanded;
            storage.Failed += failed;
            orders.Delivered += delivered;
            orders.Failed += failed;
            progression.LeveledUp += leveledUp;

            _dispose = () =>
            {
                placement.BuildingPlaced -= placed;
                town.BuildingMoved -= moved;
                town.BuildingRemoved -= removed;
                farming.Planted -= planted;
                farming.Harvested -= harvested;
                farming.Failed -= failed;
                production.Queued -= queued;
                production.Collected -= collected;
                production.Failed -= failed;
                construction.Completed -= built;
                construction.Failed -= failed;
                storage.Sold -= sold;
                storage.Expanded -= expanded;
                storage.Failed -= failed;
                orders.Delivered -= delivered;
                orders.Failed -= failed;
                progression.LeveledUp -= leveledUp;
            };
        }

        public void Dispose() => _dispose();

        private void Play(SoundId id)
        {
            if (_audio != null) _audio.Play(id);
        }
    }
}
