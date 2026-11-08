namespace TownBuilder.Domain
{
    /// <summary>
    /// Buying extra production queue slots for one factory. The n-th extra slot costs n × base cost
    /// (250, 500, 750 with the default content). Values come from GameContent.
    /// </summary>
    public static class QueueUpgrades
    {
        /// <summary>Coin cost of the next extra slot when <paramref name="owned"/> extra slots are already bought.</summary>
        public static long NextCost(int owned, int baseCost) => (long)baseCost * (System.Math.Max(0, owned) + 1);

        public static bool CanUpgrade(int owned, int maxExtra) => owned < maxExtra;
    }
}
