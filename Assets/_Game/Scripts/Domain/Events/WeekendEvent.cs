using System;
using System.Collections.Generic;
using System.Linq;

namespace TownBuilder.Domain
{
    /// <summary>
    /// Weekend calendar on local day indices (days since 1970-01-01 in the player's time zone, see
    /// <see cref="DailyRewardTrack.LocalDay"/>). 1970-01-01 was a Thursday.
    /// </summary>
    public static class WeekendCalendar
    {
        /// <summary>0 = Sunday ... 6 = Saturday.</summary>
        public static int DayOfWeek(long day) => (int)(((day + 4) % 7 + 7) % 7);

        public static bool IsWeekend(long day)
        {
            int d = DayOfWeek(day);
            return d == 6 || d == 0;
        }

        /// <summary>The Saturday that starts this weekend, or <see cref="long.MinValue"/> on a weekday.</summary>
        public static long WeekendKey(long day)
        {
            int d = DayOfWeek(day);
            return d == 6 ? day : d == 0 ? day - 1 : long.MinValue;
        }

        /// <summary>The next Saturday on or after <paramref name="day"/>.</summary>
        public static long NextSaturday(long day) => day + (6 - DayOfWeek(day));

        /// <summary>Which event of a rotation of <paramref name="count"/> runs on the weekend starting at <paramref name="key"/>.</summary>
        public static int RotationIndex(long key, int count)
        {
            if (count <= 0) return -1;
            long week = key >= 0 ? key / 7 : -((-key + 6) / 7);
            return (int)(((week % count) + count) % count);
        }
    }

    /// <summary>
    /// Progress in one time-limited event: points towards milestone goals (ascending), claimed in order.
    /// <see cref="Begin"/> starts a new event (a different key) and resets everything.
    /// </summary>
    public sealed class EventProgress
    {
        public const long NoEvent = long.MinValue;

        private List<int> _goals = new List<int>();

        public long Key { get; private set; } = NoEvent;
        public string EventId { get; private set; }
        public int Points { get; private set; }
        /// <summary>Milestones claimed so far (milestone i can be claimed when i == Claimed).</summary>
        public int Claimed { get; private set; }
        public IReadOnlyList<int> Goals => _goals;

        /// <summary>A milestone's goal was reached (index).</summary>
        public event Action<int> MilestoneReached;

        /// <summary>Starts (or continues) the event with this key. Returns true when a new event started (progress reset).</summary>
        public bool Begin(long key, string eventId, IEnumerable<int> goals)
        {
            _goals = (goals ?? Enumerable.Empty<int>()).ToList();
            if (key == Key && eventId == EventId) return false;
            Key = key;
            EventId = eventId;
            Points = 0;
            Claimed = 0;
            return true;
        }

        public void AddPoints(int amount)
        {
            if (amount <= 0 || Key == NoEvent) return;
            int before = Points;
            Points += amount;
            for (int i = Claimed; i < _goals.Count; i++)
                if (before < _goals[i] && Points >= _goals[i]) MilestoneReached?.Invoke(i);
        }

        public bool CanClaim(int index) => index == Claimed && index < _goals.Count && Points >= _goals[index];

        public bool IsComplete => _goals.Count > 0 && Claimed >= _goals.Count;

        /// <summary>Goal of the next unclaimed milestone, or -1 when all are claimed.</summary>
        public int NextGoal => Claimed < _goals.Count ? _goals[Claimed] : -1;

        public bool TryClaim(int index)
        {
            if (!CanClaim(index)) return false;
            Claimed++;
            return true;
        }

        /// <summary>Saved state (goals come from content via <see cref="Begin"/>).</summary>
        public void Restore(long key, string eventId, int points, int claimed)
        {
            Key = key;
            EventId = eventId;
            Points = Math.Max(0, points);
            Claimed = Math.Max(0, claimed);
        }
    }
}
