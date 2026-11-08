using System;
using System.Linq;
using NUnit.Framework;
using TownBuilder.Domain;

namespace TownBuilder.Tests
{
    public sealed class TaskBoardTests
    {
        private static readonly TaskSpec[] Catalog =
        {
            new TaskSpec("guide_wheat", TaskKind.Harvest, "wheat", 4),
            new TaskSpec("guide_bakery", TaskKind.Build, "bakery", 1),
            new TaskSpec("guide_corn", TaskKind.Harvest, "corn", 3, unlockLevel: 2),
            new TaskSpec("rep_bread", TaskKind.Collect, "bread", 2, repeatable: true),
            new TaskSpec("rep_orders", TaskKind.Deliver, null, 2, repeatable: true),
        };

        [Test]
        public void Fill_PutsGuideTasksFirst_InOrder_RespectingLevel()
        {
            var board = new TaskBoard(3);
            board.Fill(Catalog, 1, new Random(1));
            Assert.AreEqual("guide_wheat", board.Get(0).Spec.Id);
            Assert.AreEqual("guide_bakery", board.Get(1).Spec.Id);
            Assert.IsTrue(board.Get(2).Spec.Repeatable, "level-2 guide task skipped, a repeatable one fills the slot");
        }

        [Test]
        public void Record_AdvancesOnlyMatchingTasks_AndCapsAtGoal()
        {
            var board = new TaskBoard(3);
            board.Fill(Catalog, 1, new Random(1));
            int completed = -1;
            board.TaskCompleted += i => completed = i;

            Assert.AreEqual(1, board.Record(TaskKind.Harvest, "wheat", 2));
            Assert.AreEqual(0, board.Record(TaskKind.Harvest, "corn", 5), "corn doesn't count for wheat");
            Assert.AreEqual(2, board.Get(0).Progress);
            board.Record(TaskKind.Harvest, "wheat", 10);
            Assert.AreEqual(4, board.Get(0).Progress, "capped at the goal");
            Assert.IsTrue(board.Get(0).IsComplete);
            Assert.AreEqual(0, completed);
        }

        [Test]
        public void AnyTarget_MatchesEveryId()
        {
            var spec = new TaskSpec("any_collect", TaskKind.Collect, null, 3);
            Assert.IsTrue(spec.Matches(TaskKind.Collect, "bread"));
            Assert.IsTrue(spec.Matches(TaskKind.Collect, "cheese"));
            Assert.IsFalse(spec.Matches(TaskKind.Harvest, "bread"));
        }

        [Test]
        public void Claim_OnlyWhenComplete_ThenGuideTaskNeverReturns()
        {
            var board = new TaskBoard(1);
            board.Fill(Catalog, 1, new Random(1));
            Assert.IsFalse(board.TryClaim(0, out _), "not complete yet");

            board.Record(TaskKind.Harvest, "wheat", 4);
            Assert.IsTrue(board.TryClaim(0, out var spec));
            Assert.AreEqual("guide_wheat", spec.Id);
            Assert.IsNull(board.Get(0));

            board.Fill(Catalog, 1, new Random(1));
            Assert.AreEqual("guide_bakery", board.Get(0).Spec.Id);
        }

        [Test]
        public void AlreadyDoneGuideTasks_AreSkippedAndCountAsCompleted()
        {
            var board = new TaskBoard(1);
            board.Fill(Catalog, 2, new Random(1), s => s.Id == "guide_wheat");
            Assert.AreEqual("guide_bakery", board.Get(0).Spec.Id);
            CollectionAssert.Contains(board.Completed.ToList(), "guide_wheat");
        }

        [Test]
        public void RepeatableTasks_NeverAppearTwiceAtOnce()
        {
            var board = new TaskBoard(3);
            var repeatables = Catalog.Where(s => s.Repeatable).ToArray();
            board.Fill(repeatables, 1, new Random(7));
            var ids = board.Slots.Where(t => t != null).Select(t => t.Spec.Id).ToList();
            Assert.AreEqual(ids.Count, ids.Distinct().Count());
            Assert.AreEqual(2, ids.Count, "only 2 distinct repeatables exist, third slot stays empty");
        }

        [Test]
        public void Restore_KeepsProgressAndCompletedIds()
        {
            var board = new TaskBoard(2);
            board.Restore(1, Catalog[0], 3);
            board.RestoreCompleted(new[] { "guide_bakery" });
            Assert.AreEqual(3, board.Get(1).Progress);
            board.Fill(Catalog, 2, new Random(1));
            Assert.AreEqual("guide_corn", board.Get(0).Spec.Id, "wheat is active, bakery done, so corn is next");
        }
    }
}
