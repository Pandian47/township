using System;

namespace TownBuilder.Domain
{
    public enum Season
    {
        Spring,
        Summer,
        Autumn,
        Winter,
    }

    /// <summary>
    /// Which season it is from the phone's local date (northern-hemisphere months): spring March–May, summer June–August,
    /// autumn September–November, winter December–February.
    /// </summary>
    public static class SeasonCalendar
    {
        public static Season FromMonth(int month)
        {
            switch (month)
            {
                case 3: case 4: case 5: return Season.Spring;
                case 6: case 7: case 8: return Season.Summer;
                case 9: case 10: case 11: return Season.Autumn;
                default: return Season.Winter;
            }
        }

        /// <summary>The season at a UTC time in a time zone <paramref name="utcOffsetMs"/> away from UTC.</summary>
        public static Season At(long utcUnixMs, long utcOffsetMs) =>
            FromMonth(DateTimeOffset.FromUnixTimeMilliseconds(utcUnixMs + utcOffsetMs).UtcDateTime.Month);

        public static Season Next(Season season) => (Season)(((int)season + 1) % 4);

        public static string Name(Season season) => season.ToString();
    }
}
