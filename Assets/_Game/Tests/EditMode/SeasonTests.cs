using System;
using NUnit.Framework;
using TownBuilder.Domain;

namespace TownBuilder.Tests
{
    public sealed class SeasonTests
    {
        [TestCase(1, Season.Winter)]
        [TestCase(2, Season.Winter)]
        [TestCase(3, Season.Spring)]
        [TestCase(5, Season.Spring)]
        [TestCase(6, Season.Summer)]
        [TestCase(8, Season.Summer)]
        [TestCase(9, Season.Autumn)]
        [TestCase(11, Season.Autumn)]
        [TestCase(12, Season.Winter)]
        public void Month_GivesTheSeason(int month, Season season) => Assert.AreEqual(season, SeasonCalendar.FromMonth(month));

        [Test]
        public void At_UsesTheLocalDate()
        {
            long feb28Late = new DateTimeOffset(2026, 2, 28, 23, 30, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
            Assert.AreEqual(Season.Winter, SeasonCalendar.At(feb28Late, 0), "still February in UTC");
            Assert.AreEqual(Season.Spring, SeasonCalendar.At(feb28Late, 3_600_000), "already 1 March one hour east");
            Assert.AreEqual(Season.Winter, SeasonCalendar.At(feb28Late, -5 * 3_600_000));
        }

        [Test]
        public void Next_CyclesThroughTheYear()
        {
            Assert.AreEqual(Season.Summer, SeasonCalendar.Next(Season.Spring));
            Assert.AreEqual(Season.Spring, SeasonCalendar.Next(Season.Winter));
        }
    }
}
