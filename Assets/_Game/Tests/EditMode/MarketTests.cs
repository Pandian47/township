using NUnit.Framework;
using TownBuilder.Domain;

namespace TownBuilder.Tests
{
    public sealed class MarketTests
    {
        [Test]
        public void Sell_MovesItemsToCoins()
        {
            var inv = new Inventory(50);
            inv.TryAdd("wheat", 10);
            var wallet = new Wallet(0, 0);

            Assert.AreEqual(SellResult.Sold, Market.TrySell(inv, wallet, "wheat", 4, 2, out long earned));
            Assert.AreEqual(8, earned);
            Assert.AreEqual(8, wallet.Coins);
            Assert.AreEqual(6, inv.Count("wheat"));
        }

        [Test]
        public void Sell_MoreThanOwned_ChangesNothing()
        {
            var inv = new Inventory(50);
            inv.TryAdd("wheat", 3);
            var wallet = new Wallet(5, 0);

            Assert.AreEqual(SellResult.NotEnoughItems, Market.TrySell(inv, wallet, "wheat", 4, 2, out _));
            Assert.AreEqual(5, wallet.Coins);
            Assert.AreEqual(3, inv.Count("wheat"));
        }

        [Test]
        public void Sell_UnsellableOrZero_IsRejected()
        {
            var inv = new Inventory(50);
            inv.TryAdd("gem_ore", 3);
            var wallet = new Wallet(0, 0);
            Assert.AreEqual(SellResult.NotSellable, Market.TrySell(inv, wallet, "gem_ore", 1, 0, out _));
            Assert.AreEqual(SellResult.InvalidAmount, Market.TrySell(inv, wallet, "gem_ore", 0, 5, out _));
        }

        [Test]
        public void StorageUpgrades_CostGrows_AndCapacityAdds()
        {
            var upgrades = new StorageUpgrades(0, 25, 200, 3);
            var wallet = new Wallet(1000, 0);

            Assert.AreEqual(200, upgrades.NextCost);
            Assert.IsTrue(upgrades.TryUpgrade(wallet));
            Assert.AreEqual(25, upgrades.BonusCapacity);
            Assert.AreEqual(400, upgrades.NextCost);
            Assert.AreEqual(800, wallet.Coins);
        }

        [Test]
        public void StorageUpgrades_NotEnoughCoins_OrMaxed_Fails()
        {
            var poor = new StorageUpgrades(0, 25, 200, 3);
            Assert.IsFalse(poor.TryUpgrade(new Wallet(199, 0)));
            Assert.AreEqual(0, poor.Level);

            var maxed = new StorageUpgrades(3, 25, 200, 3);
            Assert.IsTrue(maxed.IsMaxed);
            Assert.IsFalse(maxed.TryUpgrade(new Wallet(100000, 0)));
        }

        [Test]
        public void StorageUpgrades_SavedLevelAboveMax_IsClamped()
        {
            Assert.AreEqual(3, new StorageUpgrades(99, 25, 200, 3).Level);
        }
    }
}
