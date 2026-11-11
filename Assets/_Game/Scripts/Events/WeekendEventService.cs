using System;
using System.Collections.Generic;
using System.Linq;
using TownBuilder.Domain;

namespace TownBuilder
{
    /// <summary>
    /// Weekend events in the Town scene. Every Saturday and Sunday (phone's local time) one event from
    /// GameContent → Weekend events runs (they take turns, week by week). Harvesting / making / delivering earns points;
    /// prizes are claimed in order; the last one unlocks an event-only decoration in the Build shop for good.
    /// Plain C#, owned by TownSceneController.
    /// </summary>
    public sealed class WeekendEventService : IDisposable
    {
        public const long DayMs = 86_400_000L;

        private readonly EventProgress _progress;
        private readonly HashSet<string> _unlockedDecorations;
        private readonly GameContent _content;
        private readonly Wallet _wallet;
        private readonly IClock _clock;
        private readonly Func<long> _utcOffsetMs;
        private readonly Action _unsubscribe;
        private long _devKey = EventProgress.NoEvent;

        public WeekendEventService(EventProgress progress, IEnumerable<string> unlockedDecorations, GameContent content, Wallet wallet,
            IClock clock, Func<long> utcOffsetMs, FarmingController farming, ProductionController production, OrderService orders)
        {
            _progress = progress;
            _unlockedDecorations = new HashSet<string>(unlockedDecorations ?? Enumerable.Empty<string>());
            _content = content;
            _wallet = wallet;
            _clock = clock;
            _utcOffsetMs = utcOffsetMs ?? (() => 0L);

            Action<WorldReward> harvested = r => Earn(AchievementKind.HarvestCrops, r.Items.Sum(i => Math.Max(0, i.Amount)));
            Action<WorldReward> collected = r => Earn(AchievementKind.MakeProducts, r.Items.Sum(i => Math.Max(0, i.Amount)));
            Action<int> delivered = _ => Earn(AchievementKind.DeliverOrders, 1);
            Action<int> reached = OnMilestoneReached;
            farming.Rewarded += harvested;
            production.Rewarded += collected;
            orders.Delivered += delivered;
            progress.MilestoneReached += reached;
            _unsubscribe = () =>
            {
                farming.Rewarded -= harvested;
                production.Rewarded -= collected;
                orders.Delivered -= delivered;
                progress.MilestoneReached -= reached;
            };

            Tick();
        }

        public event Action<string> Notification;
        public event Action Modified;
        /// <summary>A prize can now be claimed (milestone index).</summary>
        public event Action<int> MilestoneReady;
        /// <summary>A prize was claimed (milestone index).</summary>
        public event Action<int> Claimed;
        /// <summary>An event started or ended (show / hide the event button).</summary>
        public event Action ActiveChanged;

        public EventProgress Progress => _progress;
        public IReadOnlyCollection<string> UnlockedDecorations => _unlockedDecorations;
        public long Today => DailyRewardTrack.LocalDay(_clock.UtcNowUnixMs, _utcOffsetMs());

        /// <summary>The event running now, or null on weekdays (and when no events are set up).</summary>
        public EventDefinition Current { get; private set; }
        public bool IsActive => Current != null;

        /// <summary>Time left until the event ends (Monday 00:00 local).</summary>
        public long RemainingMs => IsActive ? Math.Max(0, (CurrentKey + 2) * DayMs - (_clock.UtcNowUnixMs + _utcOffsetMs())) : 0;

        /// <summary>Days until next Saturday (0 on a weekend).</summary>
        public long DaysUntilNext => IsActive ? 0 : WeekendCalendar.NextSaturday(Today) - Today;

        public int ClaimableCount => IsActive && _progress.CanClaim(_progress.Claimed) ? 1 : 0;

        private long CurrentKey => _devKey != EventProgress.NoEvent && Today < _devKey + 2 ? _devKey : WeekendCalendar.WeekendKey(Today);

        /// <summary>Event-only decorations are listed in the shop once a prize unlocked them.</summary>
        public bool IsListedInShop(BuildingDefinition def) => def == null || !def.EventOnly || _unlockedDecorations.Contains(def.Id);

        /// <summary>Checks the calendar: starts this weekend's event or ends it. Call now and then (the UI does every few seconds).</summary>
        public void Tick()
        {
            var events = _content.WeekendEvents.Where(e => e != null && e.Milestones.Count > 0).ToList();
            long key = CurrentKey;
            EventDefinition now = null;
            if (key != long.MinValue && events.Count > 0)
            {
                // A DEV-started event on a weekday uses the next weekend's event.
                long rotationKey = WeekendCalendar.IsWeekend(key) ? key : WeekendCalendar.NextSaturday(key);
                now = events[WeekendCalendar.RotationIndex(rotationKey, events.Count)];
            }

            bool changed = now != Current;
            Current = now;
            if (now != null && _progress.Begin(key, now.Id, now.Goals))
            {
                Notification?.Invoke($"{now.DisplayName} has started! Tap the event button to see the prizes.");
                Modified?.Invoke();
            }

            if (changed) ActiveChanged?.Invoke();
        }

        /// <summary>Claims the next prize.</summary>
        public bool Claim(int index)
        {
            Tick();
            if (!IsActive || !_progress.TryClaim(index)) return false;
            var m = Current.Milestones[index];
            if (m.coins > 0) _wallet.AddCoins(m.coins);
            if (m.gems > 0) _wallet.AddGems(m.gems);
            string extra = "";
            if (m.decoration != null && _unlockedDecorations.Add(m.decoration.Id))
                extra = $"   {m.decoration.DisplayName} is now in Build → Decorations!";
            Notification?.Invoke($"{Current.DisplayName} prize!   +{m.RewardText()}{extra}");
            Claimed?.Invoke(index);
            Modified?.Invoke();
            return true;
        }

        /// <summary>Developer mode: runs an event from today for two days (test on any weekday).</summary>
        public void DevStartNow()
        {
            _devKey = Today;
            Tick();
        }

        public void Dispose() => _unsubscribe();

        private void Earn(AchievementKind kind, int amount)
        {
            Tick();
            if (!IsActive || Current.PointsFrom != kind || amount <= 0) return;
            _progress.AddPoints(amount);
            Modified?.Invoke();
        }

        private void OnMilestoneReached(int index)
        {
            if (Current != null)
                Notification?.Invoke($"{Current.DisplayName}: prize {index + 1} reached! Tap the event button to claim.");
            MilestoneReady?.Invoke(index);
        }
    }
}
