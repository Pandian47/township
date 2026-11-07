using System;
using System.Collections.Generic;
using TownBuilder.Domain;

namespace TownBuilder
{
    public readonly struct LevelUpInfo
    {
        public LevelUpInfo(int level, long coins, long gems, IReadOnlyList<string> unlocks)
        {
            Level = level;
            Coins = coins;
            Gems = gems;
            Unlocks = unlocks;
        }

        public int Level { get; }
        public long Coins { get; }
        public long Gems { get; }
        public IReadOnlyList<string> Unlocks { get; }
    }

    /// <summary>Pays level-up rewards and tells the UI what each new level unlocked.</summary>
    public sealed class ProgressionService : IDisposable
    {
        private readonly PlayerProgress _progress;
        private readonly Wallet _wallet;
        private readonly GameContent _content;

        public ProgressionService(PlayerProgress progress, Wallet wallet, GameContent content)
        {
            _progress = progress;
            _wallet = wallet;
            _content = content;
            _progress.LeveledUp += OnLeveledUp;
        }

        public event Action<LevelUpInfo> LeveledUp;

        public void Dispose() => _progress.LeveledUp -= OnLeveledUp;

        private void OnLeveledUp(int level)
        {
            long coins = (long)_content.LevelUpCoinsPerLevel * level;
            long gems = _content.LevelUpGems;
            if (coins > 0) _wallet.AddCoins(coins);
            if (gems > 0) _wallet.AddGems(gems);
            LeveledUp?.Invoke(new LevelUpInfo(level, coins, gems, _content.UnlocksAtLevel(level)));
        }
    }
}
