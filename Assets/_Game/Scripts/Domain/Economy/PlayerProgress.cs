using System;

namespace TownBuilder.Domain
{
    /// <summary>Player level and XP. Level-up rules arrive with the progression milestone; for now it only holds state.</summary>
    public sealed class PlayerProgress
    {
        public PlayerProgress(int level, long xp)
        {
            Level = level < 1 ? 1 : level;
            Xp = xp < 0 ? 0 : xp;
        }

        public event Action Changed;

        public int Level { get; private set; }
        public long Xp { get; private set; }

        public void AddXp(long amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount == 0) return;
            Xp = checked(Xp + amount);
            Changed?.Invoke();
        }

        public bool IsUnlocked(int requiredLevel) => Level >= requiredLevel;
    }
}
