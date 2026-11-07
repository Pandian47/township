using NUnit.Framework;
using TownBuilder.Domain;

namespace TownBuilder.Tests
{
    public sealed class ConstructionSitesTests
    {
        private const long T0 = 1_700_000_000_000;

        [Test]
        public void Start_TracksProgress_UntilDone()
        {
            var sites = new ConstructionSites();
            sites.Start("barn1", T0, 60_000);
            Assert.IsTrue(sites.IsUnderConstruction("barn1"));
            sites.TryGet("barn1", out var site);
            Assert.AreEqual(0.5f, site.Progress(T0 + 30_000), 0.001f);
            Assert.AreEqual(30_000, site.RemainingMs(T0 + 30_000));
            Assert.IsFalse(site.IsDone(T0 + 59_999));
            Assert.IsTrue(site.IsDone(T0 + 60_000));
        }

        [Test]
        public void ZeroDuration_IsNeverASite()
        {
            var sites = new ConstructionSites();
            sites.Start("field1", T0, 0);
            Assert.IsFalse(sites.IsUnderConstruction("field1"));
        }

        [Test]
        public void TakeFinished_ReturnsOnlyFinished_Once()
        {
            var sites = new ConstructionSites();
            sites.Start("a", T0, 10_000);
            sites.Start("b", T0, 50_000);

            CollectionAssert.AreEqual(new[] { "a" }, sites.TakeFinished(T0 + 20_000));
            Assert.IsEmpty(sites.TakeFinished(T0 + 20_000), "already taken");
            Assert.IsTrue(sites.IsUnderConstruction("b"));
        }

        [Test]
        public void FinishNow_MakesSiteDone()
        {
            var sites = new ConstructionSites();
            sites.Start("a", T0, 120_000);
            Assert.IsTrue(sites.FinishNow("a", T0 + 1000));
            sites.TryGet("a", out var site);
            Assert.IsTrue(site.IsDone(T0 + 1000));
        }

        [Test]
        public void Restore_FromEndTime_ContinuesWhileClosed()
        {
            var sites = new ConstructionSites();
            sites.Restore("a", T0 + 60_000, 60_000);
            sites.TryGet("a", out var site);
            Assert.AreEqual(T0, site.StartUnixMs);
            Assert.AreEqual(1, sites.TakeFinished(T0 + 3_600_000).Count);
        }

        [TestCase(1, 1)]
        [TestCase(60_000, 1)]
        [TestCase(60_001, 2)]
        [TestCase(150_000, 3)]
        public void GemCost_IsOnePerStartedMinute(long remainingMs, int gems)
        {
            Assert.AreEqual(gems, ConstructionSites.GemCostToFinish(remainingMs));
        }

        [Test]
        public void Mapper_WritesConstructionEnd()
        {
            var town = new Town(new GridRect(0, 0, 10, 10));
            town.TryAdd("barn1", "barn", new GridCoord(1, 1), new GridCoord(2, 2), out _);
            town.TryAdd("hall", "town_hall", new GridCoord(5, 5), new GridCoord(3, 3), out _);
            var sites = new ConstructionSites();
            sites.Start("barn1", T0, 60_000);
            var save = NewGameFactory.Create(T0, 0, 0);

            TownSaveMapper.WriteTo(save, town, new Wallet(0, 0), new PlayerProgress(1, 0), sites: sites);

            Assert.AreEqual(T0 + 60_000, save.buildings.Find(b => b.instanceId == "barn1").constructionEndsAtUnixMs);
            Assert.AreEqual(0, save.buildings.Find(b => b.instanceId == "hall").constructionEndsAtUnixMs);
        }
    }
}
