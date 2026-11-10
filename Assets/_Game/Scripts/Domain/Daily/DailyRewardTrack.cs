using System;

namespace TownBuilder.Domain
{
    /// <summary>
    /// The daily reward calendar: one claim per local calendar day. Claiming on consecutive days walks through the
    /// calendar (day 1, 2, ... N, then day 1 again); missing a day starts over at day 1.
    /// Days are whole-number day indices (e.g. days since 1970 in the player's time zone), supplied by the caller.
    /// </summary>
    public sealed class DailyRewardTrack
    {
        public const long NeverClaimed = long.MinValue;

        public DailyRewardTrack(int calendarLength, long lastClaimDay = NeverClaimed, int streak = 0)
        {
            CalendarLength = Math.Max(1, calendarLength);
            LastClaimDay = lastClaimDay;
            Streak = Math.Max(0, streak);
        }

        public int CalendarLength { get; }
        public long LastClaimDay { get; private set; }

        /// <summary>Consecutive days claimed so far (0 = never / chain broken).</summary>
        public int Streak { get; private set; }

        public bool CanClaim(long today) => LastClaimDay == NeverClaimed || today > LastClaimDay;

        /// <summary>True when the last claim was yesterday or today (the chain continues).</summary>
        public bool StreakAlive(long today) => LastClaimDay != NeverClaimed && today - LastClaimDay <= 1;

        /// <summary>Calendar slot (0-based) that today's claim pays, or would pay.</summary>
        public int NextIndex(long today)
        {
            int nextStreak = StreakAlive(today) && CanClaim(today) ? Streak + 1 : 1;
            if (!CanClaim(today)) nextStreak = Streak; // already claimed today: show today's slot
            return (Math.Max(1, nextStreak) - 1) % CalendarLength;
        }

        /// <summary>Claims today's reward. Returns the calendar slot (0-based) to pay, or -1 if already claimed today.</summary>
        public int Claim(long today)
        {
            if (!CanClaim(today)) return -1;
            Streak = StreakAlive(today) ? Streak + 1 : 1;
            LastClaimDay = today;
            return (Streak - 1) % CalendarLength;
        }

        /// <summary>Sets the saved state (load, developer tools).</summary>
        public void Restore(long lastClaimDay, int streak)
        {
            LastClaimDay = lastClaimDay;
            Streak = Math.Max(0, streak);
        }

        /// <summary>Converts a UTC time plus the device's time-zone offset into a local day index.</summary>
        public static long LocalDay(long utcUnixMs, long utcOffsetMs)
        {
            long local = utcUnixMs + utcOffsetMs;
            const long dayMs = 86_400_000L;
            return local >= 0 ? local / dayMs : -((-local + dayMs - 1) / dayMs);
        }
    }
}
