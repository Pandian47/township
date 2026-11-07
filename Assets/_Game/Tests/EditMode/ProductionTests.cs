using NUnit.Framework;
using TownBuilder.Domain;

namespace TownBuilder.Tests
{
    public sealed class ProductionQueueTests
    {
        private const long T0 = 1_700_000_000_000;
        private const long ThreeMin = 180_000;

        [Test]
        public void Jobs_RunOneAfterAnother()
        {
            var q = new ProductionQueue(3);
            Assert.IsTrue(q.TryEnqueue("bread", ThreeMin, T0));
            Assert.IsTrue(q.TryEnqueue("bread", ThreeMin, T0 + 1000));

            Assert.AreEqual(T0, q.Jobs[0].StartUnixMs);
            Assert.AreEqual(T0 + ThreeMin, q.Jobs[1].StartUnixMs, "second job starts when the first ends");
            Assert.AreSame(q.Jobs[0], q.ActiveJob(T0 + 1000));
            Assert.AreSame(q.Jobs[1], q.ActiveJob(T0 + ThreeMin + 1));
            Assert.AreEqual(2, q.CountDone(T0 + 2 * ThreeMin));
        }

        [Test]
        public void Enqueue_WhenIdle_StartsNow()
        {
            var q = new ProductionQueue(3);
            q.TryEnqueue("bread", ThreeMin, T0);
            // First finished long ago, nothing collected; a new job queued later starts at queue time.
            q.TryEnqueue("bread", ThreeMin, T0 + 10 * ThreeMin);
            Assert.AreEqual(T0 + 10 * ThreeMin, q.Jobs[1].StartUnixMs);
        }

        [Test]
        public void Queue_RespectsSlots_IncludingUncollected()
        {
            var q = new ProductionQueue(2);
            q.TryEnqueue("a", ThreeMin, T0);
            q.TryEnqueue("b", ThreeMin, T0);
            Assert.IsTrue(q.IsFull);
            Assert.IsFalse(q.TryEnqueue("c", ThreeMin, T0 + 10 * ThreeMin), "finished-but-uncollected jobs still use slots");
        }

        [Test]
        public void TakeFinished_ReturnsOldestOnlyWhenDone()
        {
            var q = new ProductionQueue(3);
            q.TryEnqueue("a", ThreeMin, T0);
            q.TryEnqueue("b", ThreeMin, T0);
            Assert.IsNull(q.TryTakeFinished(T0 + 1000));
            Assert.AreEqual("a", q.TryTakeFinished(T0 + ThreeMin).RecipeId);
            Assert.IsNull(q.TryTakeFinished(T0 + ThreeMin), "b is not done yet");
            Assert.AreEqual(1, q.Jobs.Count);
        }

        [Test]
        public void FinishActiveNow_CompletesActive_AndPullsLaterJobsForward()
        {
            var q = new ProductionQueue(3);
            q.TryEnqueue("a", ThreeMin, T0);
            q.TryEnqueue("b", ThreeMin, T0);
            long now = T0 + 60_000; // 2 minutes saved on "a"

            Assert.IsTrue(q.FinishActiveNow(now));
            Assert.IsTrue(q.Jobs[0].IsDone(now));
            Assert.AreEqual(now, q.Jobs[1].StartUnixMs, "b starts right away");
            Assert.AreSame(q.Jobs[1], q.ActiveJob(now));
        }

        [Test]
        public void FinishActiveNow_WithNothingActive_Fails()
        {
            var q = new ProductionQueue(3);
            Assert.IsFalse(q.FinishActiveNow(T0));
            q.TryEnqueue("a", ThreeMin, T0);
            Assert.IsFalse(q.FinishActiveNow(T0 + ThreeMin), "already done");
        }

        [Test]
        public void ProductionContinuesWhileClosed()
        {
            var q = new ProductionQueue(3);
            q.Restore("a", T0, ThreeMin);
            q.Restore("b", T0 + ThreeMin, ThreeMin);
            Assert.AreEqual(2, q.CountDone(T0 + 3_600_000));
        }

        [Test]
        public void Mapper_RoundTripsJobsInOrder()
        {
            var factories = new Factories();
            var q = factories.GetOrCreate("bakery1", 3);
            q.TryEnqueue("bake_bread", ThreeMin, T0);
            q.TryEnqueue("bake_corn_bread", 360_000, T0);
            var save = NewGameFactory.Create(T0, 0, 0);

            TownSaveMapper.WriteProduction(save, factories);

            Assert.AreEqual(2, save.productionJobs.Count);
            Assert.AreEqual("bake_bread", save.productionJobs[0].recipeId);
            Assert.AreEqual(T0 + ThreeMin, save.productionJobs[1].startUnixMs);
            Assert.AreEqual("bakery1", save.productionJobs[1].factoryInstanceId);
        }
    }
}
