using System;
using System.Collections.Generic;
using NUnit.Framework;
using TownBuilder.Domain;

namespace TownBuilder.Tests
{
    public sealed class AchievementBookTests
    {
        private static AchievementBook NewBook() => new AchievementBook(new[]
        {
            new AchievementSpec("harvest", AchievementKind.HarvestCrops, new[] { 10, 50, 200 }),
            new AchievementSpec("level", AchievementKind.ReachLevel, new[] { 5, 10 }),
        });

        [Test]
        public void Counting_ReachesGoal_RaisesOnce_ThenClaimsInOrder()
        {
            var book = NewBook();
            var reached = new List<string>();
            book.GoalReached += reached.Add;

            book.Add(AchievementKind.HarvestCrops, 6);
            Assert.IsFalse(book.CanClaim("harvest"));
            book.Add(AchievementKind.HarvestCrops, 6);
            book.Add(AchievementKind.HarvestCrops, 1);
            Assert.AreEqual(new[] { "harvest" }, reached.ToArray(), "raised when crossing the goal, not again after");
            Assert.IsTrue(book.CanClaim("harvest"));
            Assert.AreEqual(1, book.ClaimableCount);

            Assert.IsTrue(book.TryClaim("harvest", out int tier));
            Assert.AreEqual(0, tier);
            Assert.AreEqual(1, book.ClaimedTiers("harvest"));
            Assert.AreEqual(50, book.NextGoal("harvest"));
            Assert.IsFalse(book.CanClaim("harvest"), "13 of 50");
            Assert.IsFalse(book.TryClaim("harvest", out _));
        }

        [Test]
        public void BigJump_CrossingSeveralGoals_ClaimsOneTierAtATime()
        {
            var book = NewBook();
            book.Add(AchievementKind.HarvestCrops, 500);
            Assert.IsTrue(book.TryClaim("harvest", out _));
            Assert.IsTrue(book.TryClaim("harvest", out _));
            Assert.IsTrue(book.TryClaim("harvest", out int last));
            Assert.AreEqual(2, last);
            Assert.IsTrue(book.IsFinished("harvest"));
            Assert.AreEqual(-1, book.NextGoal("harvest"));
            Assert.IsFalse(book.CanClaim("harvest"));
        }

        [Test]
        public void RaiseTo_NeverLowers()
        {
            var book = NewBook();
            book.RaiseTo(AchievementKind.ReachLevel, 6);
            book.RaiseTo(AchievementKind.ReachLevel, 3);
            Assert.AreEqual(6, book.Count(AchievementKind.ReachLevel));
            Assert.IsTrue(book.CanClaim("level"));
        }

        [Test]
        public void ClaimedGoalCrossedAgain_DoesNotRaise()
        {
            var book = NewBook();
            book.Add(AchievementKind.HarvestCrops, 10);
            book.TryClaim("harvest", out _);
            var reached = new List<string>();
            book.GoalReached += reached.Add;
            book.Add(AchievementKind.HarvestCrops, 39);
            Assert.IsEmpty(reached, "49 of 50");
            book.Add(AchievementKind.HarvestCrops, 1);
            Assert.AreEqual(new[] { "harvest" }, reached.ToArray());
        }

        [Test]
        public void Restore_IgnoresUnknownIds_AndClampsTiers()
        {
            var book = NewBook();
            book.Restore(
                new[] { new KeyValuePair<AchievementKind, long>(AchievementKind.HarvestCrops, 70) },
                new[] { new KeyValuePair<string, int>("harvest", 1), new KeyValuePair<string, int>("level", 9), new KeyValuePair<string, int>("gone", 2) });
            Assert.AreEqual(70, book.Count(AchievementKind.HarvestCrops));
            Assert.AreEqual(1, book.ClaimedTiers("harvest"));
            Assert.IsTrue(book.CanClaim("harvest"), "70 of 50");
            Assert.AreEqual(2, book.ClaimedTiers("level"), "clamped to its 2 tiers");
            Assert.IsTrue(book.IsFinished("level"));
            Assert.AreEqual(0, book.ClaimedTiers("gone"));
        }

        [Test]
        public void Spec_RejectsBadGoals()
        {
            Assert.Throws<ArgumentException>(() => new AchievementSpec("x", AchievementKind.LoadCrates, new int[0]));
            Assert.Throws<ArgumentException>(() => new AchievementSpec("x", AchievementKind.LoadCrates, new[] { 10, 10 }));
            Assert.Throws<ArgumentException>(() => new AchievementSpec("x", AchievementKind.LoadCrates, new[] { 0, 5 }));
        }
    }
}
