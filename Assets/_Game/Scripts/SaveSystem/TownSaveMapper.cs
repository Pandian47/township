using System.Collections.Generic;
using TownBuilder.Domain;

namespace TownBuilder
{
    /// <summary>Converts between the save snapshot (DTOs) and the live domain objects of the Town scene.</summary>
    public static class TownSaveMapper
    {
        public static Wallet CreateWallet(SaveData save) => new Wallet(save.player.coins, save.player.gems);

        public static PlayerProgress CreateProgress(SaveData save) => new PlayerProgress(save.player.level, save.player.xp);

        /// <summary>Adds saved buildings to the town. Entries whose definition no longer exists or no longer fit are skipped.</summary>
        public static void LoadBuildings(SaveData save, Town town, GameContent content, List<string> warnings)
        {
            foreach (var entry in save.buildings)
            {
                if (!content.TryGetBuilding(entry.definitionId, out var definition))
                {
                    warnings.Add($"Unknown building '{entry.definitionId}' skipped.");
                    continue;
                }

                var result = town.TryAdd(entry.instanceId, entry.definitionId, new GridCoord(entry.x, entry.y),
                    definition.FootprintSize, out _);
                if (result != PlacementResult.Valid)
                    warnings.Add($"Building '{entry.definitionId}' at ({entry.x},{entry.y}) skipped: {result}.");
            }
        }

        public static void WriteTo(SaveData save, Town town, Wallet wallet, PlayerProgress progress)
        {
            save.player.coins = wallet.Coins;
            save.player.gems = wallet.Gems;
            save.player.level = progress.Level;
            save.player.xp = progress.Xp;

            save.buildings.Clear();
            foreach (var b in town.Buildings)
            {
                save.buildings.Add(new BuildingSaveData
                {
                    instanceId = b.InstanceId,
                    definitionId = b.DefinitionId,
                    x = b.Origin.X,
                    y = b.Origin.Y,
                });
            }
        }
    }
}
