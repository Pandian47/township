using System;
using System.Collections.Generic;

namespace TownBuilder.Domain
{
    /// <summary>
    /// XP needed to go from each level to the next. Entry 0 = XP from level 1 to 2, entry 1 = level 2 to 3, ...
    /// Past the end of the table each level needs <see cref="ExtraPerLevelAfterTable"/> more than the previous one.
    /// XP in the save is the TOTAL earned, so the curve can be rebalanced without breaking saves.
    /// </summary>
    public sealed class LevelCurve
    {
        private readonly int[] _xpToNext;

        public LevelCurve(IReadOnlyList<int> xpToNext, int extraPerLevelAfterTable = 100, int maxLevel = 99)
        {
            if (xpToNext == null || xpToNext.Count == 0) throw new ArgumentException("Curve needs at least one entry.", nameof(xpToNext));
            _xpToNext = new int[xpToNext.Count];
            for (int i = 0; i < xpToNext.Count; i++) _xpToNext[i] = Math.Max(1, xpToNext[i]);
            ExtraPerLevelAfterTable = Math.Max(0, extraPerLevelAfterTable);
            MaxLevel = Math.Max(1, maxLevel);
        }

        public int ExtraPerLevelAfterTable { get; }
        public int MaxLevel { get; }

        /// <summary>XP needed to go from <paramref name="level"/> to level + 1.</summary>
        public long XpToNext(int level)
        {
            if (level < 1) level = 1;
            int index = level - 1;
            if (index < _xpToNext.Length) return _xpToNext[index];
            long last = _xpToNext[_xpToNext.Length - 1];
            return last + (long)(index - _xpToNext.Length + 1) * ExtraPerLevelAfterTable;
        }

        /// <summary>Total XP at which <paramref name="level"/> is reached (level 1 = 0).</summary>
        public long TotalXpForLevel(int level)
        {
            long total = 0;
            for (int l = 1; l < level; l++) total += XpToNext(l);
            return total;
        }

        public int LevelForTotalXp(long totalXp)
        {
            int level = 1;
            long needed = 0;
            while (level < MaxLevel)
            {
                needed += XpToNext(level);
                if (totalXp < needed) break;
                level++;
            }

            return level;
        }

        /// <summary>A simple default used by tests and as a fallback.</summary>
        public static LevelCurve Default => new LevelCurve(new[] { 15, 30, 50, 80, 120, 170, 230, 300, 380, 470 });
    }
}
