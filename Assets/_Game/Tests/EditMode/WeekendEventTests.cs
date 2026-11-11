using System.Collections.Generic;
using NUnit.Framework;
using TownBuilder.Domain;

namespace TownBuilder.Tests
{
    public sealed class WeekendEventTests
    {
        // Local day indices (days since 1970-01-01): 2026-11-14 is a Saturday.
        private const long Saturday = 20771;

        [Test]
        public void Calendar_KnowsTheWeekdays()
        {
            Assert.AreEqual(4, WeekendCalendar.DayOfWeek(0), "1970-01-01 was a Thursday");
            Assert.AreEqual(6, WeekendCalendar.DayOfWeek(Saturday));
            Assert.AreEqual(0, WeekendCalendar.DayOfWeek(Saturday + 1));
            Assert.IsTrue(WeekendCalendar.IsWeekend(Saturday));
            Assert.IsTrue(WeekendCalendar.IsWeekend(Saturday + 1));
            Assert.IsFalse(WeekendCalendar.IsWeekend(Saturday + 2), "Monday");
            Assert.IsFalse(WeekendCalendar.IsWeekend(Saturday - 1), "Friday");
        }

        [Test]
        public void WeekendKey_IsTheSaturday_ForBothDays()
        {
            Assert.AreEqual(Saturday, WeekendCalendar.WeekendKey(Saturday));
            Assert.AreEqual(Saturday, WeekendCalendar.WeekendKey(Saturday + 1));
            Assert.AreEqual(long.MinValue, WeekendCalendar.WeekendKey(Saturday + 3));
            Assert.AreEqual(Saturday, WeekendCalendar.NextSaturday(Saturday - 3));
            Assert.AreEqual(Saturday, WeekendCalendar.NextSaturday(Saturday));
        }

        [Test]
        public void Rotation_ChangesEveryWeek_AndWraps()
        {
            int a = WeekendCalendar.RotationIndex(Saturday, 2);
            int b = WeekendCalendar.RotationIndex(Saturday + 7, 2);
            int c = WeekendCalendar.RotationIndex(Saturday + 14, 2);
            Assert.AreNotEqual(a, b);
            Assert.AreEqual(a, c);
            Assert.AreEqual(-1, WeekendCalendar.RotationIndex(Saturday, 0));
        }

        [Test]
        public void Progress_ReachesMilestones_ClaimsInOrder()
        {
            var p = new EventProgress();
            var reached = new List<int>();
            p.MilestoneReached += reached.Add;
            Assert.IsTrue(p.Begin(Saturday, "harvest_festival", new[] { 10, 30, 60 }));

            p.AddPoints(35);
            CollectionAssert.AreEqual(new[] { 0, 1 }, reached);
            Assert.IsTrue(p.CanClaim(0));
            Assert.IsFalse(p.CanClaim(1), "in order");
            Assert.IsTrue(p.TryClaim(0));
            Assert.IsTrue(p.TryClaim(1));
            Assert.IsFalse(p.TryClaim(2), "35 of 60");
            Assert.AreEqual(60, p.NextGoal);

            p.AddPoints(25);
            Assert.IsTrue(p.TryClaim(2));
            Assert.IsTrue(p.IsComplete);
            Assert.AreEqual(-1, p.NextGoal);
        }

        [Test]
        public void Begin_SameEventKeepsProgress_NewWeekendResets()
        {
            var p = new EventProgress();
            p.Begin(Saturday, "harvest_festival", new[] { 10 });
            p.AddPoints(7);
            Assert.IsFalse(p.Begin(Saturday, "harvest_festival", new[] { 10 }), "same weekend: continue");
            Assert.AreEqual(7, p.Points);

            Assert.IsTrue(p.Begin(Saturday + 7, "market_weekend", new[] { 5 }));
            Assert.AreEqual(0, p.Points);
            Assert.AreEqual(0, p.Claimed);
        }

        [Test]
        public void NoEvent_IgnoresPoints_AndRestoreWorks()
        {
            var p = new EventProgress();
            p.AddPoints(5);
            Assert.AreEqual(0, p.Points);

            p.Restore(Saturday, "harvest_festival", 42, 1);
            Assert.IsFalse(p.Begin(Saturday, "harvest_festival", new[] { 10, 50 }));
            Assert.AreEqual(42, p.Points);
            Assert.AreEqual(1, p.Claimed);
            Assert.IsFalse(p.CanClaim(1), "42 of 50");
        }
    }
}
