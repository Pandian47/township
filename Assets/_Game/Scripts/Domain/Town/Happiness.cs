using System;
using System.Collections.Generic;

namespace TownBuilder.Domain
{
    /// <summary>A decoration's effect on nearby homes: where it stands and how much happiness it gives.</summary>
    public readonly struct HappinessSource
    {
        public HappinessSource(GridCoord origin, GridCoord size, int value)
        {
            Origin = origin;
            Size = size;
            Value = value;
        }

        public GridCoord Origin { get; }
        public GridCoord Size { get; }
        public int Value { get; }
    }

    /// <summary>
    /// How happy a home is (0-100): the sum of the happiness of every decoration within <c>radius</c> cells of its
    /// footprint, capped at 100.
    /// </summary>
    public static class TownHappiness
    {
        public const int Max = 100;

        /// <summary>Gap in cells between two footprints (0 = touching or overlapping), measured like a king's move.</summary>
        public static int Distance(GridCoord aOrigin, GridCoord aSize, GridCoord bOrigin, GridCoord bSize)
        {
            int gapX = Math.Max(0, Math.Max(bOrigin.X - (aOrigin.X + aSize.X - 1), aOrigin.X - (bOrigin.X + bSize.X - 1)) - 1);
            int gapY = Math.Max(0, Math.Max(bOrigin.Y - (aOrigin.Y + aSize.Y - 1), aOrigin.Y - (bOrigin.Y + bSize.Y - 1)) - 1);
            return Math.Max(gapX, gapY);
        }

        public static int ForHome(GridCoord origin, GridCoord size, IEnumerable<HappinessSource> sources, int radius)
        {
            int sum = 0;
            foreach (var s in sources)
                if (s.Value > 0 && Distance(origin, size, s.Origin, s.Size) <= radius) sum += s.Value;
            return Math.Min(Max, sum);
        }

        /// <summary>Coins per hour of a home: the base rate, up to twice as much at 100 % happiness.</summary>
        public static double CoinsPerHour(int baseRate, int happiness) =>
            Math.Max(0, baseRate) * (1.0 + Math.Max(0, Math.Min(Max, happiness)) / (double)Max);
    }

    /// <summary>
    /// Coins that homes collect over time. Each home remembers when it was last emptied; it fills at its current rate
    /// up to <c>maxHours</c> worth. UTC times, so homes keep earning while the app is closed.
    /// </summary>
    public sealed class HomeCoins
    {
        private readonly Dictionary<string, long> _lastCollected = new Dictionary<string, long>();

        public IReadOnlyDictionary<string, long> LastCollected => _lastCollected;

        /// <summary>Starts tracking a home (a new home starts empty). True when it was new.</summary>
        public bool Track(string homeId, long nowUnixMs)
        {
            if (string.IsNullOrEmpty(homeId) || _lastCollected.ContainsKey(homeId)) return false;
            _lastCollected[homeId] = nowUnixMs;
            return true;
        }

        public bool Forget(string homeId) => homeId != null && _lastCollected.Remove(homeId);

        /// <summary>Coins waiting in a home now (whole coins).</summary>
        public int Pending(string homeId, long nowUnixMs, double coinsPerHour, double maxHours)
        {
            if (homeId == null || !_lastCollected.TryGetValue(homeId, out var last)) return 0;
            double hours = Math.Min(Math.Max(0, maxHours), Math.Max(0, nowUnixMs - last) / 3_600_000.0);
            return (int)Math.Floor(hours * Math.Max(0, coinsPerHour));
        }

        /// <summary>Empties a home. Returns the coins it held.</summary>
        public int Collect(string homeId, long nowUnixMs, double coinsPerHour, double maxHours)
        {
            int coins = Pending(homeId, nowUnixMs, coinsPerHour, maxHours);
            if (homeId != null && _lastCollected.ContainsKey(homeId)) _lastCollected[homeId] = nowUnixMs;
            return coins;
        }

        public void Restore(string homeId, long lastCollectedUnixMs)
        {
            if (!string.IsNullOrEmpty(homeId)) _lastCollected[homeId] = lastCollectedUnixMs;
        }
    }
}
