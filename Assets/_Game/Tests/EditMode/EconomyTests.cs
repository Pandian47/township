using System;
using NUnit.Framework;
using TownBuilder.Domain;

namespace TownBuilder.Tests
{
    public sealed class EconomyTests
    {
        [Test]
        public void SpendCoins_WithEnoughBalance_Deducts()
        {
            var wallet = new Wallet(100, 0);
            int changes = 0;
            wallet.Changed += () => changes++;

            Assert.IsTrue(wallet.TrySpendCoins(40));
            Assert.AreEqual(60, wallet.Coins);
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void SpendCoins_WithoutEnoughBalance_FailsAndKeepsBalance()
        {
            var wallet = new Wallet(10, 0);
            Assert.IsFalse(wallet.TrySpendCoins(11));
            Assert.AreEqual(10, wallet.Coins);
        }

        [Test]
        public void NegativeAmounts_Throw()
        {
            var wallet = new Wallet(10, 10);
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.TrySpendCoins(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.AddCoins(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.AddGems(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Wallet(-1, 0));
        }

        [Test]
        public void Progress_ClampsInvalidValues()
        {
            var p = new PlayerProgress(0, -5);
            Assert.AreEqual(1, p.Level);
            Assert.AreEqual(0, p.Xp);
            Assert.IsTrue(p.IsUnlocked(1));
            Assert.IsFalse(p.IsUnlocked(2));
        }
    }
}
