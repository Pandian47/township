using System;

namespace TownBuilder.Domain
{
    /// <summary>
    /// The price of skipping a wait with gems, the same everywhere (construction, crops and fruit, factory jobs):
    /// 1 gem per started minute left, at least 1.
    /// </summary>
    public static class TimeSkip
    {
        public static int GemCost(long remainingMs) => Math.Max(1, (int)Math.Ceiling(Math.Max(0, remainingMs) / 60000.0));
    }
}
