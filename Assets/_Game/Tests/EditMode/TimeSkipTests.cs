using NUnit.Framework;
using TownBuilder.Domain;

namespace TownBuilder.Tests
{
    public sealed class TimeSkipTests
    {
        [TestCase(0L, 1)]
        [TestCase(1L, 1)]
        [TestCase(59_999L, 1)]
        [TestCase(60_000L, 1)]
        [TestCase(60_001L, 2)]
        [TestCase(30 * 60_000L, 30)]
        [TestCase(-5L, 1)]
        public void GemCost_IsOnePerStartedMinute_AtLeastOne(long remainingMs, int gems) =>
            Assert.AreEqual(gems, TimeSkip.GemCost(remainingMs));

        [Test]
        public void Construction_UsesTheSamePrice() =>
            Assert.AreEqual(TimeSkip.GemCost(125_000), ConstructionSites.GemCostToFinish(125_000));
    }
}
