using System;

namespace TownBuilder.Domain
{
    /// <summary>
    /// Source of "now" for every timer (construction, crops, factories). Timers store absolute UTC timestamps,
    /// never countdowns, so they keep running while the app is closed. Swap for a server clock when online features arrive.
    /// </summary>
    public interface IClock
    {
        long UtcNowUnixMs { get; }
    }

    public sealed class SystemClock : IClock
    {
        public long UtcNowUnixMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    /// <summary>Manually controlled clock for tests.</summary>
    public sealed class ManualClock : IClock
    {
        public ManualClock(long startUnixMs) => UtcNowUnixMs = startUnixMs;
        public long UtcNowUnixMs { get; set; }
        public void Advance(long milliseconds) => UtcNowUnixMs += milliseconds;
    }
}
