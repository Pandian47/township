using NUnit.Framework;
using TownBuilder.Domain;

namespace TownBuilder.Tests
{
    public sealed class QueueUpgradeTests
    {
        [Test]
        public void Cost_GrowsWithEachSlot()
        {
            Assert.AreEqual(250, QueueUpgrades.NextCost(0, 250));
            Assert.AreEqual(500, QueueUpgrades.NextCost(1, 250));
            Assert.AreEqual(750, QueueUpgrades.NextCost(2, 250));
            Assert.AreEqual(250, QueueUpgrades.NextCost(-3, 250), "corrupt value treated as none bought");
        }

        [Test]
        public void CanUpgrade_UntilMax()
        {
            Assert.IsTrue(QueueUpgrades.CanUpgrade(0, 3));
            Assert.IsTrue(QueueUpgrades.CanUpgrade(2, 3));
            Assert.IsFalse(QueueUpgrades.CanUpgrade(3, 3));
            Assert.IsFalse(QueueUpgrades.CanUpgrade(0, 0), "upgrades can be switched off in content");
        }

        [Test]
        public void Factories_ApplyExtraSlots_ToNewAndExistingQueues()
        {
            var factories = new Factories();
            var queue = factories.GetOrCreate("bakery1", 3);
            Assert.AreEqual(3, queue.Slots);

            factories.SetExtraSlots("bakery1", 2);
            Assert.AreEqual(5, factories.GetOrCreate("bakery1", 3).Slots);
            Assert.AreSame(queue, factories.GetOrCreate("bakery1", 3), "same queue, just bigger");

            factories.SetExtraSlots("mill1", 1);
            Assert.AreEqual(4, factories.GetOrCreate("mill1", 3).Slots, "upgrade known before the queue exists");
        }

        [Test]
        public void ExtraSlot_CanBeFilled()
        {
            var factories = new Factories();
            var queue = factories.GetOrCreate("f", 1);
            Assert.IsTrue(queue.TryEnqueue("r", 1000, 0));
            Assert.IsTrue(queue.IsFull);
            factories.SetExtraSlots("f", 1);
            factories.GetOrCreate("f", 1);
            Assert.IsFalse(queue.IsFull);
            Assert.IsTrue(queue.TryEnqueue("r", 1000, 0));
        }

        [Test]
        public void RemovingFactory_ForgetsItsUpgrades()
        {
            var factories = new Factories();
            factories.SetExtraSlots("f", 2);
            Assert.IsTrue(factories.Remove("f"));
            Assert.AreEqual(0, factories.ExtraSlots("f"));
            Assert.AreEqual(3, factories.GetOrCreate("f", 3).Slots);
        }

        [Test]
        public void Sanitize_ResetsNegativeUpgrades()
        {
            var data = NewGameFactory.Create(0, 100, 0);
            data.buildings.Add(new BuildingSaveData { instanceId = "b", definitionId = "bakery", extraQueueSlots = -2 });
            SaveMigrator.Sanitize(data);
            Assert.AreEqual(0, data.buildings.Find(b => b.instanceId == "b").extraQueueSlots);
        }
    }
}
