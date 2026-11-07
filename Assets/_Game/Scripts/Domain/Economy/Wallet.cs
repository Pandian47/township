using System;

namespace TownBuilder.Domain
{
    /// <summary>Soft (coins) and premium (gems) currency balances. All changes go through this class.</summary>
    public sealed class Wallet
    {
        public Wallet(long coins, long gems)
        {
            if (coins < 0) throw new ArgumentOutOfRangeException(nameof(coins));
            if (gems < 0) throw new ArgumentOutOfRangeException(nameof(gems));
            Coins = coins;
            Gems = gems;
        }

        public event Action Changed;

        public long Coins { get; private set; }
        public long Gems { get; private set; }

        public bool CanAffordCoins(long amount) => amount <= Coins;

        public bool TrySpendCoins(long amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount > Coins) return false;
            if (amount == 0) return true;
            Coins -= amount;
            Changed?.Invoke();
            return true;
        }

        public void AddCoins(long amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount == 0) return;
            Coins = checked(Coins + amount);
            Changed?.Invoke();
        }

        public bool TrySpendGems(long amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount > Gems) return false;
            if (amount == 0) return true;
            Gems -= amount;
            Changed?.Invoke();
            return true;
        }

        public void AddGems(long amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount == 0) return;
            Gems = checked(Gems + amount);
            Changed?.Invoke();
        }
    }
}
