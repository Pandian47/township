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
            OrderService orders, ProgressionService progression, TaskService tasks = null, TrainController train = null, LandController land = null)
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
            Action<string> slotAdded = _ => Play(SoundId.BuildComplete);
            Action<string> sold = _ => Play(SoundId.Coins);
            Action expanded = () => Play(SoundId.BuildComplete);
            Action<int> delivered = _ => Play(SoundId.Coins);
            Action<LevelUpInfo> leveledUp = _ => Play(SoundId.LevelUp);
            Action failed = () => Play(SoundId.Error);
            Action<int> taskDone = _ => Play(SoundId.Collect);
            Action<int> taskClaimed = _ => Play(SoundId.Coins);
            Action trainArrived = () => Play(SoundId.BuildComplete);
            Action<int> crateFilled = _ => Play(SoundId.Coins);
            Action<bool> trainDeparted = _ => Play(SoundId.Place);
            Action<PlotCoord> landBought = _ => Play(SoundId.BuildComplete);

            placement.BuildingPlaced += placed;
            town.BuildingMoved += moved;
            town.BuildingRemoved += removed;
            farming.Planted += planted;
            farming.Harvested += harvested;
            farming.Failed += failed;
            production.Queued += queued;
            production.Collected += collected;
            production.Failed += failed;
            production.SlotAdded += slotAdded;
            construction.Completed += built;
            construction.Failed += failed;
            storage.Sold += sold;
            storage.Expanded += expanded;
            storage.Failed += failed;
            orders.Delivered += delivered;
            orders.Failed += failed;
            progression.LeveledUp += leveledUp;
            if (tasks != null)
            {
                tasks.Completed += taskDone;
                tasks.Claimed += taskClaimed;
            }

            if (train != null)
            {
                train.Arrived += trainArrived;
                train.CrateFilled += crateFilled;
                train.Departed += trainDeparted;
                train.Failed += failed;
            }

            if (land != null)
            {
                land.Bought += landBought;
                land.Failed += failed;
            }

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
                production.SlotAdded -= slotAdded;
                construction.Completed -= built;
                construction.Failed -= failed;
                storage.Sold -= sold;
                storage.Expanded -= expanded;
                storage.Failed -= failed;
                orders.Delivered -= delivered;
                orders.Failed -= failed;
                progression.LeveledUp -= leveledUp;
                if (tasks != null)
                {
                    tasks.Completed -= taskDone;
                    tasks.Claimed -= taskClaimed;
                }

                if (train != null)
                {
                    train.Arrived -= trainArrived;
                    train.CrateFilled -= crateFilled;
                    train.Departed -= trainDeparted;
                    train.Failed -= failed;
                }

                if (land != null)
                {
                    land.Bought -= landBought;
                    land.Failed -= failed;
                }
            };
        }

        private System.Action _disposeDaily;
        private System.Action _disposeAchievements;
        private System.Action _disposeEvents;
        private System.Action _disposeTruck;
        private System.Action _disposeHomes;
        private System.Action _disposeCollections;

        /// <summary>Collection sounds: a find, a completed set.</summary>
        public void AddCollections(CollectionService collections)
        {
            if (collections == null) return;
            System.Action<string, string> found = (_, __) => Play(SoundId.Collect);
            System.Action<string> completed = _ => Play(SoundId.LevelUp);
            collections.Found += found;
            collections.Completed += completed;
            _disposeCollections = () =>
            {
                collections.Found -= found;
                collections.Completed -= completed;
            };
        }

        /// <summary>Coins sound when a home's coins are collected.</summary>
        public void AddHomes(HappinessController homes)
        {
            if (homes == null) return;
            System.Action<string, int> collected = (_, __) => Play(SoundId.Coins);
            homes.Collected += collected;
            _disposeHomes = () => homes.Collected -= collected;
        }

        /// <summary>Truck sounds (the truck is set up after the bridge).</summary>
        public void AddTruck(TruckController truck)
        {
            if (truck == null) return;
            System.Action<int> loaded = _ => Play(SoundId.Collect);
            System.Action<bool> departed = _ => Play(SoundId.BuildComplete);
            System.Action<TruckPayout> returned = _ => Play(SoundId.Coins);
            System.Action failed = () => Play(SoundId.Error);
            truck.Loaded += loaded;
            truck.Departed += departed;
            truck.Returned += returned;
            truck.Failed += failed;
            _disposeTruck = () =>
            {
                truck.Loaded -= loaded;
                truck.Departed -= departed;
                truck.Returned -= returned;
                truck.Failed -= failed;
            };
        }

        /// <summary>Weekend event sounds (the service is created after the bridge).</summary>
        public void AddEvents(WeekendEventService events)
        {
            if (events == null) return;
            System.Action<int> ready = _ => Play(SoundId.Collect);
            System.Action<int> claimed = _ => Play(SoundId.LevelUp);
            events.MilestoneReady += ready;
            events.Claimed += claimed;
            _disposeEvents = () =>
            {
                events.MilestoneReady -= ready;
                events.Claimed -= claimed;
            };
        }

        /// <summary>Achievement sounds (the service is created after the bridge).</summary>
        public void AddAchievements(AchievementService achievements)
        {
            if (achievements == null) return;
            System.Action<string> ready = _ => Play(SoundId.Collect);
            System.Action<string> claimed = _ => Play(SoundId.LevelUp);
            achievements.Ready += ready;
            achievements.Claimed += claimed;
            _disposeAchievements = () =>
            {
                achievements.Ready -= ready;
                achievements.Claimed -= claimed;
            };
        }

        /// <summary>Daily reward sound (the daily service is created after the bridge).</summary>
        public void AddDaily(DailyRewardService daily)
        {
            if (daily == null) return;
            System.Action<int> claimed = _ => Play(SoundId.LevelUp);
            daily.Claimed += claimed;
            _disposeDaily = () => daily.Claimed -= claimed;
        }

        public void Dispose()
        {
            _dispose();
            _disposeDaily?.Invoke();
            _disposeAchievements?.Invoke();
            _disposeEvents?.Invoke();
            _disposeTruck?.Invoke();
            _disposeHomes?.Invoke();
            _disposeCollections?.Invoke();
        }

        private void Play(SoundId id)
        {
            if (_audio != null) _audio.Play(id);
        }
    }
}
