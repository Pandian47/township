using System.Collections.Generic;

namespace TownBuilder
{
    public readonly struct ItemGain
    {
        public readonly ItemDefinition Item;
        public readonly int Amount;

        public ItemGain(ItemDefinition item, int amount)
        {
            Item = item;
            Amount = amount;
        }
    }

    /// <summary>
    /// What a player action on a building earned (harvest, collect, construction finished).
    /// Raised by gameplay controllers so effects can show it in the world ("+2 Wheat  +1 XP").
    /// </summary>
    public sealed class WorldReward
    {
        public WorldReward(string buildingId, IReadOnlyList<ItemGain> items, int xp, int coins = 0)
        {
            BuildingId = buildingId;
            Items = items ?? System.Array.Empty<ItemGain>();
            Xp = xp;
            Coins = coins;
        }

        public string BuildingId { get; }
        public IReadOnlyList<ItemGain> Items { get; }
        public int Xp { get; }
        public int Coins { get; }
    }
}
