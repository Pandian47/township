using NUnit.Framework;
using TownBuilder.Domain;

namespace TownBuilder.Tests
{
    /// <summary>Milestone 7 step 3: fruit trees regrow after every harvest.</summary>
    public sealed class FruitTreeTests
    {
        private const long Grow = 30 * 60 * 1000L;

        [Test]
        public void HarvestAndRegrow_KeepsTheCrop_AndStartsGrowingAgain()
        {
            var plots = new FarmPlots();
            plots.TryPlant("tree1", "apple", Grow, 1000);

            Assert.IsNull(plots.TryHarvestAndRegrow("tree1", 1000 + Grow - 1), "not ripe yet");
            Assert.AreEqual("apple", plots.TryHarvestAndRegrow("tree1", 1000 + Grow + 500));

            Assert.AreEqual(PlotStage.Growing, plots.GetStage("tree1", 1000 + Grow + 500), "grows again straight away");
            Assert.IsTrue(plots.TryGetCrop("tree1", out var crop));
            Assert.AreEqual(1000 + Grow + 500, crop.PlantedAtUnixMs);
            Assert.AreEqual(PlotStage.Ready, plots.GetStage("tree1", 1000 + 2 * Grow + 500), "ripe again one grow time later");
        }

        [Test]
        public void HarvestAndRegrow_EmptyPlot_ReturnsNull()
        {
            var plots = new FarmPlots();
            Assert.IsNull(plots.TryHarvestAndRegrow("nothing", 0));
        }

        [Test]
        public void HarvestAndRegrow_RaisesPlotChanged()
        {
            var plots = new FarmPlots();
            plots.TryPlant("tree1", "cherry", Grow, 0);
            int changed = 0;
            plots.PlotChanged += _ => changed++;
            plots.TryHarvestAndRegrow("tree1", Grow);
            Assert.AreEqual(1, changed);
        }
    }
}
