using System.Linq;
using TownBuilder.Domain;

namespace TownBuilder
{
    /// <summary>
    /// Testing shortcuts ("developer mode"). Only available in the Unity Editor and in test APKs built with
    /// TOWNBUILDER_DEVTOOLS (BuildTools.BuildApk). Google Play bundles (AAB) are built without it, so players never see it.
    /// Each action goes through the normal game systems (rewards, events, saving), so it behaves like real play.
    /// </summary>
    public static class DevTools
    {
#if UNITY_EDITOR || TOWNBUILDER_DEVTOOLS
        public const bool Enabled = true;
#else
        public const bool Enabled = false;
#endif

        /// <summary>Level that unlocks all current content.</summary>
        public const int UnlockAllLevel = 10;

        public static void AddCurrency(TownSceneController scene, long coins = 10_000, long gems = 100)
        {
            scene.Wallet.AddCoins(coins);
            scene.Wallet.AddGems(gems);
            scene.SaveNow();
        }

        /// <summary>Exactly one level up (with the normal level-up reward and dialog).</summary>
        public static void LevelUp(TownSceneController scene)
        {
            var p = scene.Progress;
            if (p.IsMaxLevel) return;
            p.AddXp(System.Math.Max(1, p.XpForNextLevel - p.XpIntoLevel));
            scene.SaveNow();
        }

        /// <summary>Levels up until <see cref="UnlockAllLevel"/>: every building, crop, recipe, land ring and the train open.</summary>
        public static void UnlockAll(TownSceneController scene)
        {
            for (int i = 0; i < 50 && scene.Progress.Level < UnlockAllLevel && !scene.Progress.IsMaxLevel; i++)
                LevelUp(scene);
        }

        /// <summary>Barn expanded to the maximum, then up to 10 of every item (crops, products, materials) while there's room.</summary>
        public static void FillBarn(TownSceneController scene, GameContent content, int each = 10)
        {
            var storage = scene.Storage;
            for (int i = 0; i < 50 && !storage.Upgrades.IsMaxed; i++)
            {
                scene.Wallet.AddCoins(storage.Upgrades.NextCost);
                if (!storage.TryExpand()) break;
            }

            var ids = content.Crops.Select(c => c.Id)
                .Concat(content.Products.Select(p => p.Id))
                .Concat(content.Materials.Select(m => m.Id))
                .Where(id => !string.IsNullOrEmpty(id));
            foreach (var id in ids)
            {
                int amount = System.Math.Min(each, scene.Inventory.FreeSpace);
                if (amount <= 0) break;
                scene.Inventory.TryAdd(id, amount);
            }

            scene.SaveNow();
        }

        /// <summary>Crops ready, factories done, construction finished, train here.</summary>
        public static void FinishAllTimers(TownSceneController scene)
        {
            foreach (var plot in scene.Farming.Plots.Crops.Keys.ToList()) scene.Farming.FinishNow(plot);

            foreach (var factory in scene.Production.Factories.Queues.Keys.ToList())
                for (int i = 0; i < 20 && scene.Production.FinishActiveNow(factory); i++) { }

            foreach (var site in scene.Construction.Sites.Sites.Keys.ToList()) scene.Construction.FinishNow(site);

            if (scene.Train != null) scene.Train.CallNow();
            scene.SaveNow();
        }

        /// <summary>Owns every land plot (free).</summary>
        public static void UnlockAllLand(TownSceneController scene)
        {
            var land = scene.Land;
            for (int i = 0; i < 100; i++)
            {
                var forSale = land.PlotsForSale().ToList();
                if (forSale.Count == 0) break;
                foreach (var plot in forSale) land.Buy(plot);
            }

            scene.SaveNow();
        }
    }
}
