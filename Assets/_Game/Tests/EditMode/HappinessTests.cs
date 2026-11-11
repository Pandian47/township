using NUnit.Framework;
using TownBuilder.Domain;

namespace TownBuilder.Tests
{
    public sealed class HappinessTests
    {
        private static readonly GridCoord Two = new GridCoord(2, 2);
        private static readonly GridCoord One = new GridCoord(1, 1);

        [Test]
        public void Distance_CountsTheGapBetweenFootprints()
        {
            var home = new GridCoord(10, 10); // covers 10-11 x 10-11
            Assert.AreEqual(0, TownHappiness.Distance(home, Two, new GridCoord(12, 10), One), "touching");
            Assert.AreEqual(0, TownHappiness.Distance(home, Two, new GridCoord(11, 11), One), "overlapping");
            Assert.AreEqual(2, TownHappiness.Distance(home, Two, new GridCoord(14, 11), One));
            Assert.AreEqual(3, TownHappiness.Distance(home, Two, new GridCoord(6, 6), One), "diagonal: the bigger gap");
        }

        [Test]
        public void HomeHappiness_SumsNearbyDecorations_CappedAt100()
        {
            var home = new GridCoord(10, 10);
            var sources = new[]
            {
                new HappinessSource(new GridCoord(12, 10), One, 8),   // flower bed next door
                new HappinessSource(new GridCoord(14, 14), Two, 20),  // fountain 2 cells away
                new HappinessSource(new GridCoord(20, 20), One, 50),  // too far
            };
            Assert.AreEqual(28, TownHappiness.ForHome(home, Two, sources, 3));
            Assert.AreEqual(8, TownHappiness.ForHome(home, Two, sources, 1));

            var lots = new[] { new HappinessSource(new GridCoord(12, 10), One, 70), new HappinessSource(new GridCoord(9, 9), One, 70) };
            Assert.AreEqual(100, TownHappiness.ForHome(home, Two, lots, 3));
        }

        [Test]
        public void CoinsPerHour_DoublesAtFullHappiness()
        {
            Assert.AreEqual(5.0, TownHappiness.CoinsPerHour(5, 0), 1e-9);
            Assert.AreEqual(7.5, TownHappiness.CoinsPerHour(5, 50), 1e-9);
            Assert.AreEqual(10.0, TownHappiness.CoinsPerHour(5, 100), 1e-9);
            Assert.AreEqual(10.0, TownHappiness.CoinsPerHour(5, 250), 1e-9, "capped");
        }

        [Test]
        public void HomeCoins_FillOverTime_UpToTheCap_AndEmptyOnCollect()
        {
            const long hour = 3_600_000;
            var coins = new HomeCoins();
            Assert.IsTrue(coins.Track("home1", 0));
            Assert.IsFalse(coins.Track("home1", 99), "already tracked");
            Assert.AreEqual(0, coins.Pending("home1", 0, 10, 3));
            Assert.AreEqual(15, coins.Pending("home1", hour + hour / 2, 10, 3));
            Assert.AreEqual(30, coins.Pending("home1", 10 * hour, 10, 3), "full after 3 hours");

            Assert.AreEqual(30, coins.Collect("home1", 10 * hour, 10, 3));
            Assert.AreEqual(0, coins.Pending("home1", 10 * hour, 10, 3), "empty again");
            Assert.AreEqual(0, coins.Pending("unknown", 10 * hour, 10, 3));

            coins.Restore("home2", 0);
            Assert.AreEqual(10, coins.Pending("home2", hour, 10, 3));
            Assert.IsTrue(coins.Forget("home2"));
        }
    }
}
