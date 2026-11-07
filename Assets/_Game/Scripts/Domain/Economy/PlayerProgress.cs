using System;

namespace TownBuilder.Domain
{
    /// <summary>
    /// Player level and total XP. With a <see cref="LevelCurve"/>, adding XP raises the level automatically
    /// (possibly several levels at once) and fires <see cref="LeveledUp"/> once per level gained.
    /// </summary>
    public sealed class PlayerProgress
    {
        private readonly LevelCurve _curve;

        public PlayerProgress(int level, long xp, LevelCurve curve = null)
        {
            _curve = curve;
            Xp = xp < 0 ? 0 : xp;
            Level = level < 1 ? 1 : level;

            // Repair saves whose level is behind their XP (e.g. after the curve was introduced or rebalanced).
            // A level ahead of XP is kept: never take levels away from a player.
            if (_curve != null)
            {
                int byXp = _curve.LevelForTotalXp(Xp);
                if (byXp > Level) Level = byXp;
            }
        }

        public event Action Changed;

        /// <summary>Raised for every level gained, with the new level.</summary>
        public event Action<int> LeveledUp;

        public int Level { get; private set; }
        public long Xp { get; private set; }
        public bool HasCurve => _curve != null;

        /// <summary>XP earned since reaching the current level.</summary>
        public long XpIntoLevel => _curve == null ? Xp : Math.Max(0, Xp - _curve.TotalXpForLevel(Level));

        /// <summary>XP needed from the current level to the next (0 without a curve).</summary>
        public long XpForNextLevel => _curve == null ? 0 : _curve.XpToNext(Level);

        public bool IsMaxLevel => _curve != null && Level >= _curve.MaxLevel;

        public void AddXp(long amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount == 0) return;
            Xp = checked(Xp + amount);

            if (_curve != null)
            {
                while (!IsMaxLevel && Xp >= _curve.TotalXpForLevel(Level + 1))
                {
                    Level++;
                    LeveledUp?.Invoke(Level);
                }
            }

            Changed?.Invoke();
        }

        public bool IsUnlocked(int requiredLevel) => Level >= requiredLevel;
    }
}
