using NUnit.Framework;
using TownBuilder.Domain;

namespace TownBuilder.Tests
{
    public sealed class DailyRewardTrackTests
    {
        [Test]
        public void FirstEverClaim_IsDay1_AndOnlyOncePerDay()
        {
            var track = new DailyRewardTrack(7);
            Assert.IsTrue(track.CanClaim(100));
            Assert.AreEqual(0, track.NextIndex(100));
            Assert.AreEqual(0, track.Claim(100));
            Assert.IsFalse(track.CanClaim(100));
            Assert.AreEqual(-1, track.Claim(100), "second claim the same day");
            Assert.AreEqual(1, track.Streak);
        }

        [Test]
        public void ConsecutiveDays_WalkTheCalendar_AndWrapAfter7()
        {
            var track = new DailyRewardTrack(7);
            for (int d = 0; d < 7; d++) Assert.AreEqual(d, track.Claim(200 + d));
            Assert.AreEqual(7, track.Streak);
            Assert.AreEqual(0, track.NextIndex(207), "day 8 starts the calendar again");
            Assert.AreEqual(0, track.Claim(207));
            Assert.AreEqual(1, track.Claim(208));
        }

        [Test]
        public void MissingADay_RestartsAtDay1()
        {
            var track = new DailyRewardTrack(7);
            track.Claim(10);
            track.Claim(11);
            track.Claim(12);
            Assert.AreEqual(3, track.NextIndex(13));
            Assert.AreEqual(0, track.NextIndex(14), "skipped day 13");
            Assert.AreEqual(0, track.Claim(14));
            Assert.AreEqual(1, track.Streak);
        }

        [Test]
        public void AlreadyClaimedToday_ShowsTodaysSlot()
        {
            var track = new DailyRewardTrack(7);
            track.Claim(50);
            track.Claim(51);
            Assert.AreEqual(1, track.NextIndex(51));
        }

        [Test]
        public void Restore_KeepsStreak()
        {
            var track = new DailyRewardTrack(7, lastClaimDay: 30, streak: 4);
            Assert.AreEqual(4, track.Claim(31), "day 5");
        }

        [Test]
        public void LocalDay_UsesTheTimeZone()
        {
            long day = 86_400_000L;
            // 23:30 UTC on day 10 is already day 11 in a +01:00 time zone, still day 10 in UTC.
            long t = 10 * day + 23 * 3_600_000L + 30 * 60_000L;
            Assert.AreEqual(10, DailyRewardTrack.LocalDay(t, 0));
            Assert.AreEqual(11, DailyRewardTrack.LocalDay(t, 3_600_000L));
            Assert.AreEqual(10, DailyRewardTrack.LocalDay(10 * day + 30 * 60_000L, 0));
            Assert.AreEqual(9, DailyRewardTrack.LocalDay(10 * day + 30 * 60_000L, -3_600_000L), "negative offsets");
            Assert.AreEqual(-1, DailyRewardTrack.LocalDay(-1, 0), "before 1970 rounds down");
        }
    }
}
