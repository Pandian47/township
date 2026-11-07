using System.Collections.Generic;
using NUnit.Framework;
using TownBuilder.Domain;

namespace TownBuilder.Tests
{
    public sealed class InventoryTests
    {
        [Test]
        public void Add_WithinCapacity_Succeeds()
        {
            var inv = new Inventory(10);
            Assert.IsTrue(inv.TryAdd("wheat", 4));
            Assert.IsTrue(inv.TryAdd("corn", 6));
            Assert.AreEqual(10, inv.Total);
            Assert.AreEqual(0, inv.FreeSpace);
            Assert.AreEqual(4, inv.Count("wheat"));
        }

        [Test]
        public void Add_OverCapacity_FailsAndChangesNothing()
        {
            var inv = new Inventory(5);
            inv.TryAdd("wheat", 4);
            Assert.IsFalse(inv.TryAdd("wheat", 2));
            Assert.AreEqual(4, inv.Count("wheat"));
        }

        [Test]
        public void Remove_TooMany_Fails_ExactAmount_RemovesEntry()
        {
            var inv = new Inventory(10);
            inv.TryAdd("wheat", 3);
            Assert.IsFalse(inv.TryRemove("wheat", 4));
            Assert.IsTrue(inv.TryRemove("wheat", 3));
            Assert.AreEqual(0, inv.Count("wheat"));
            Assert.IsFalse(inv.Items.ContainsKey("wheat"));
        }

        [Test]
        public void Restore_IgnoresCapacity_SoSavesNeverLoseItems()
        {
            var inv = new Inventory(5);
            inv.Restore("wheat", 8);
            Assert.AreEqual(8, inv.Total);
            Assert.IsFalse(inv.CanAdd(1));
        }

        [Test]
        public void SetCapacity_RaisesChanged()
        {
            var inv = new Inventory(5);
            int changes = 0;
            inv.Changed += () => changes++;
            inv.SetCapacity(55);
            Assert.AreEqual(55, inv.Capacity);
            Assert.AreEqual(1, changes);
        }
    }

    public sealed class FarmPlotsTests
    {
        private const long T0 = 1_700_000_000_000;
        private const long TwoMinutes = 120_000;

        [Test]
        public void Plant_ThenGrows_ThenReady()
        {
            var plots = new FarmPlots();
            Assert.AreEqual(PlotStage.Empty, plots.GetStage("f1", T0));
            Assert.IsTrue(plots.TryPlant("f1", "wheat", TwoMinutes, T0));

            Assert.AreEqual(PlotStage.Growing, plots.GetStage("f1", T0 + 60_000));
            plots.TryGetCrop("f1", out var crop);
            Assert.AreEqual(0.5f, crop.Progress(T0 + 60_000), 0.001f);
            Assert.AreEqual(60_000, crop.RemainingMs(T0 + 60_000));

            Assert.AreEqual(PlotStage.Ready, plots.GetStage("f1", T0 + TwoMinutes));
        }

        [Test]
        public void Plant_OnOccupiedPlot_Fails()
        {
            var plots = new FarmPlots();
            plots.TryPlant("f1", "wheat", TwoMinutes, T0);
            Assert.IsFalse(plots.TryPlant("f1", "corn", TwoMinutes, T0));
        }

        [Test]
        public void Harvest_BeforeReady_Fails_AfterReady_ClearsPlot()
        {
            var plots = new FarmPlots();
            plots.TryPlant("f1", "wheat", TwoMinutes, T0);
            Assert.IsNull(plots.TryHarvest("f1", T0 + 1000));
            Assert.AreEqual("wheat", plots.TryHarvest("f1", T0 + TwoMinutes));
            Assert.AreEqual(PlotStage.Empty, plots.GetStage("f1", T0 + TwoMinutes));
        }

        [Test]
        public void GrowthContinuesWhileAppClosed()
        {
            // Planted, app closed for an hour, reopened: crop must be ready.
            var plots = new FarmPlots();
            plots.Restore("f1", "wheat", T0, TwoMinutes);
            Assert.AreEqual(PlotStage.Ready, plots.GetStage("f1", T0 + 3_600_000));
        }

        [Test]
        public void ClockSetBackwards_DoesNotBreak()
        {
            var plots = new FarmPlots();
            plots.TryPlant("f1", "wheat", TwoMinutes, T0);
            plots.TryGetCrop("f1", out var crop);
            Assert.AreEqual(0f, crop.Progress(T0 - 50_000));
            Assert.AreEqual(PlotStage.Growing, plots.GetStage("f1", T0 - 50_000));
        }

        [Test]
        public void FinishNow_MakesCropReady()
        {
            var plots = new FarmPlots();
            plots.TryPlant("f1", "corn", 300_000, T0);
            Assert.IsTrue(plots.FinishNow("f1", T0 + 10_000));
            Assert.AreEqual(PlotStage.Ready, plots.GetStage("f1", T0 + 10_000));
            Assert.IsFalse(plots.FinishNow("f1", T0 + 10_000), "already ready");
        }

        [Test]
        public void Clear_RemovesCrop()
        {
            var plots = new FarmPlots();
            plots.TryPlant("f1", "wheat", TwoMinutes, T0);
            Assert.IsTrue(plots.Clear("f1"));
            Assert.AreEqual(PlotStage.Empty, plots.GetStage("f1", T0));
        }

        [Test]
        public void Mapper_WritesCropsAndInventory()
        {
            var town = new Town(new GridRect(0, 0, 10, 10));
            var plots = new FarmPlots();
            plots.TryPlant("f1", "wheat", TwoMinutes, T0);
            var inventory = new Inventory(50);
            inventory.TryAdd("corn", 7);
            var save = NewGameFactory.Create(T0, 0, 0);

            TownSaveMapper.WriteTo(save, town, new Wallet(0, 0), new PlayerProgress(1, 0), plots, inventory);

            Assert.AreEqual(1, save.cropPlots.Count);
            Assert.AreEqual("f1", save.cropPlots[0].plotInstanceId);
            Assert.AreEqual(T0, save.cropPlots[0].plantedAtUnixMs);
            Assert.AreEqual(1, save.inventory.Count);
            Assert.AreEqual(7, save.inventory[0].amount);
        }
    }
}
