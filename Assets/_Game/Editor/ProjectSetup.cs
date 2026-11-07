using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace TownBuilder.EditorTools
{
    /// <summary>
    /// One-click (or command-line) project foundation:
    ///   folders, Android player settings, placeholder art + tiles, building definitions, GameContent,
    ///   building prefab, Boot/MainMenu/Town scenes and build settings.
    /// Safe to re-run: existing art, definitions and scenes are kept unless you choose "Rebuild Scenes".
    ///
    /// Command line:
    ///   Unity.exe -batchmode -quit -projectPath &lt;path&gt; -executeMethod TownBuilder.EditorTools.ProjectSetup.RunFromCommandLine
    /// </summary>
    public static class ProjectSetup
    {
        public const string Root = "Assets/_Game";
        public const string ApplicationId = "com.pandian47.townbuilder";
        public const string CompanyName = "Pandian47";
        public const string ProductName = "TownBuilder";
        public const string ContentPath = Root + "/ScriptableObjects/GameContent.asset";
        public const string BuildingPrefabPath = Root + "/Prefabs/Buildings/BuildingView.prefab";

        private static readonly string[] Folders =
        {
            "Art/Buildings/Residential", "Art/Buildings/Production", "Art/Buildings/Community", "Art/Crops",
            "Art/Environment/Tiles/TileAssets", "Art/Characters", "Art/Vehicles", "Art/Decorations", "Art/UI",
            "Animations", "Audio", "Fonts", "Materials",
            "Prefabs/World", "Prefabs/Buildings", "Prefabs/Crops", "Prefabs/Characters", "Prefabs/UI",
            "Scenes", "Data/Buildings", "Data/Crops", "Data/Products", "Data/Recipes",
            "ScriptableObjects", "Settings", "Tests/EditMode", "Tests/PlayMode",
        };

        private struct BuildingSeed
        {
            public string Id, Name, Folder;
            public BuildingCategory Category;
            public int W, H, HeightPx, Unlock, Cost, MaxCount, StorageBonus, BuildSeconds, BuildXp;
            public bool Removable, FarmPlot;
            public Color32 Wall, Roof;
        }

        private struct CropSeed
        {
            public string Id, Name;
            public PlaceholderArtGenerator.CropStyle Style;
            public int GrowSeconds, Yield, SeedCost, Xp, Unlock, SellPrice;
        }

        // Example balance values (not taken from any existing game). Tune them in the assets.
        private static readonly CropSeed[] CropSeeds =
        {
            new CropSeed { Id = "wheat", Name = "Wheat", Style = PlaceholderArtGenerator.CropStyle.Wheat,
                GrowSeconds = 120, Yield = 2, SeedCost = 0, Xp = 1, Unlock = 1, SellPrice = 2 },
            new CropSeed { Id = "corn", Name = "Corn", Style = PlaceholderArtGenerator.CropStyle.Corn,
                GrowSeconds = 300, Yield = 3, SeedCost = 3, Xp = 2, Unlock = 2, SellPrice = 4 },
        };

        // Fields placed in a new town, next to the Town Hall.
        private static readonly Vector2Int[] StarterFieldOrigins = { new Vector2Int(21, 17), new Vector2Int(22, 17), new Vector2Int(23, 17) };

        // First content set for Milestone 1. Balance values are placeholders; tune them in the assets afterwards.
        private static readonly BuildingSeed[] Seeds =
        {
            new BuildingSeed { Id = "town_hall", Name = "Town Hall", Category = BuildingCategory.Community, Folder = "Community",
                W = 3, H = 3, HeightPx = 150, Unlock = 1, Cost = 0, Removable = false, MaxCount = 1,
                Wall = new Color32(240, 226, 196, 255), Roof = new Color32(70, 120, 190, 255) },
            new BuildingSeed { Id = "cottage", Name = "Cottage", Category = BuildingCategory.Residential, Folder = "Residential",
                W = 2, H = 2, HeightPx = 80, Unlock = 1, Cost = 150, Removable = true, BuildSeconds = 30, BuildXp = 5,
                Wall = new Color32(250, 236, 200, 255), Roof = new Color32(214, 92, 70, 255) },
            new BuildingSeed { Id = "barn", Name = "Barn", Category = BuildingCategory.Production, Folder = "Production",
                W = 2, H = 2, HeightPx = 110, Unlock = 1, Cost = 300, Removable = true, StorageBonus = 50, BuildSeconds = 60, BuildXp = 8,
                Wall = new Color32(190, 72, 56, 255), Roof = new Color32(120, 84, 64, 255) },
            new BuildingSeed { Id = "bakery", Name = "Bakery", Category = BuildingCategory.Production, Folder = "Production",
                W = 2, H = 2, HeightPx = 95, Unlock = 1, Cost = 450, Removable = true, BuildSeconds = 120, BuildXp = 10,
                Wall = new Color32(246, 214, 160, 255), Roof = new Color32(226, 140, 50, 255) },
            new BuildingSeed { Id = "silo", Name = "Silo", Category = BuildingCategory.Production, Folder = "Production",
                W = 1, H = 1, HeightPx = 140, Unlock = 3, Cost = 250, Removable = true, StorageBonus = 30, BuildSeconds = 90, BuildXp = 6,
                Wall = new Color32(186, 196, 206, 255), Roof = new Color32(120, 136, 150, 255) },
            new BuildingSeed { Id = "field", Name = "Field", Category = BuildingCategory.Farming, Folder = "Farming",
                W = 1, H = 1, Unlock = 1, Cost = 20, Removable = true, MaxCount = 12, FarmPlot = true },
        };

        [MenuItem("TownBuilder/Setup/Run Full Setup", priority = 0)]
        public static void RunFullSetupMenu()
        {
            Run(overwriteScenes: false);
            EditorUtility.DisplayDialog("TownBuilder", "Setup finished. Open Assets/_Game/Scenes/Boot.unity and press Play.", "OK");
        }

        [MenuItem("TownBuilder/Setup/Rebuild Scenes (overwrites scene edits)", priority = 1)]
        public static void RebuildScenesMenu()
        {
            if (!EditorUtility.DisplayDialog("Rebuild scenes?",
                    "Boot, MainMenu and Town will be regenerated. Manual changes to those scenes are lost.", "Rebuild", "Cancel"))
                return;
            Run(overwriteScenes: true);
        }

        [MenuItem("TownBuilder/Setup/Reimport Art", priority = 2)]
        public static void ReimportArt()
        {
            AssetDatabase.ImportAsset($"{Root}/Art", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        }

        [MenuItem("TownBuilder/Saves/Open Save Folder", priority = 20)]
        public static void OpenSaveFolder()
        {
            var dir = System.IO.Path.Combine(Application.persistentDataPath, GameRoot.SaveFolderName);
            System.IO.Directory.CreateDirectory(dir);
            EditorUtility.RevealInFinder(dir);
        }

        [MenuItem("TownBuilder/Saves/Delete Local Save", priority = 21)]
        public static void DeleteSave()
        {
            if (!EditorUtility.DisplayDialog("Delete save?", "Deletes the Editor's local town save (and its backup).", "Delete", "Cancel")) return;
            new JsonSaveStore(System.IO.Path.Combine(Application.persistentDataPath, GameRoot.SaveFolderName)).Delete();
        }

        public static void RunFromCommandLine()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                if (args.Contains("-reimportArt")) ReimportArt();
                Run(args.Contains("-rebuildScenes"));
                Debug.Log("[ProjectSetup] Completed successfully.");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static void Run(bool overwriteScenes)
        {
            CreateFolders();
            ConfigurePlayerSettings();

            var art = PlaceholderArtGenerator.Generate(Seeds.Where(s => !s.FarmPlot).Select(s => new PlaceholderArtGenerator.BuildingArtSpec
            {
                Id = s.Id, Folder = $"{PlaceholderArtGenerator.BuildingsArtFolder}/{s.Folder}", FootprintX = s.W, FootprintY = s.H,
                HeightPx = s.HeightPx, Wall = s.Wall, Roof = s.Roof,
            }));
            PlaceholderArtGenerator.EnsureFolder(PlaceholderArtGenerator.FarmingBuildingsFolder);
            PlaceholderArtGenerator.EnsureFolder(PlaceholderArtGenerator.CropsArtFolder);
            foreach (var s in Seeds.Where(s => s.FarmPlot)) art.Buildings[s.Id] = PlaceholderArtGenerator.CreateFieldSprite();
            PlaceholderArtGenerator.CreateConstructionIcon();

            CreateContent(art);
            CreateCrops();
            CreateProduction();
            ConfigureAds();
            CreateBuildingPrefab();
            AssetDatabase.SaveAssets();

            SceneBuilder.BuildAll(overwriteScenes);
            ConfigureBuildScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void CreateFolders()
        {
            foreach (var f in Folders) PlaceholderArtGenerator.EnsureFolder($"{Root}/{f}");
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            // Version is owned by Player Settings (bump it per release). Only set it on a brand-new project.
            if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion == "1.0")
                PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.colorSpace = ColorSpace.Linear;

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            var android = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(android, ApplicationId);
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(android, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.Android.bundleVersionCode = Math.Max(1, PlayerSettings.Android.bundleVersionCode);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
        }

        private static GameContent CreateContent(PlaceholderArtGenerator.Result art)
        {
            var definitions = new List<BuildingDefinition>();
            foreach (var s in Seeds)
            {
                var path = $"{Root}/Data/Buildings/Building_{s.Id}.asset";
                var def = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(path);
                if (def == null)
                {
                    def = ScriptableObject.CreateInstance<BuildingDefinition>();
                    def.EditorConfigure(s.Id, s.Name, s.Category, art.Buildings[s.Id], new Vector2Int(s.W, s.H), s.Unlock, s.Cost, s.Removable, s.MaxCount);
                    def.EditorConfigureConstruction(s.BuildSeconds, s.BuildXp);
                    AssetDatabase.CreateAsset(def, path);
                }
                else if (def.Sprite == null)
                {
                    def.EditorConfigure(def.Id, def.DisplayName, def.Category, art.Buildings[s.Id],
                        new Vector2Int(def.FootprintSize.X, def.FootprintSize.Y), def.UnlockLevel, def.CoinCost, def.Removable, def.MaxCount);
                    EditorUtility.SetDirty(def);
                }

                // Gameplay roles added in later milestones: only fill them in when still at their defaults.
                bool rolesMissing = (s.FarmPlot && !def.IsFarmPlot) || (s.StorageBonus > 0 && def.StorageCapacityBonus == 0);
                if (rolesMissing)
                {
                    def.EditorConfigureRoles(s.FarmPlot || def.IsFarmPlot, Math.Max(s.StorageBonus, def.StorageCapacityBonus));
                    EditorUtility.SetDirty(def);
                }

                definitions.Add(def);
            }

            var contentPath = ContentPath;
            var content = AssetDatabase.LoadAssetAtPath<GameContent>(contentPath);
            var field = definitions.First(d => d.Id == "field");
            if (content == null)
            {
                content = ScriptableObject.CreateInstance<GameContent>();
                var townHall = definitions.First(d => d.Id == "town_hall");
                var starters = new List<StarterBuilding> { new StarterBuilding { building = townHall, origin = new Vector2Int(18, 18) } };
                starters.AddRange(StarterFieldOrigins.Select(o => new StarterBuilding { building = field, origin = o }));
                content.EditorConfigure(definitions, starters);
                AssetDatabase.CreateAsset(content, contentPath);
                return content;
            }

            // Existing content: add new seed buildings (keep designer-added ones and their order).
            var merged = content.Buildings.Where(b => b != null).ToList();
            foreach (var def in definitions)
                if (!merged.Contains(def)) merged.Add(def);
            content.EditorSetBuildings(merged);

            if (!content.StarterBuildings.Any(s => s.building == field))
            {
                var starters = content.StarterBuildings.ToList();
                starters.AddRange(StarterFieldOrigins.Select(o => new StarterBuilding { building = field, origin = o }));
                content.EditorSetStarters(starters);
            }

            EditorUtility.SetDirty(content);
            return content;
        }

        private struct ProductSeed
        {
            public string Id, Name;
            public PlaceholderArtGenerator.ProductStyle Style;
            public int SellPrice;
        }

        private struct RecipeSeed
        {
            public string Id, ProductId, FactoryId;
            public (string item, int amount)[] Ingredients;
            public int Seconds, Xp, Unlock;
        }

        // Example values (not from any existing game).
        private static readonly ProductSeed[] ProductSeeds =
        {
            new ProductSeed { Id = "bread", Name = "Bread", Style = PlaceholderArtGenerator.ProductStyle.Bread, SellPrice = 12 },
            new ProductSeed { Id = "corn_bread", Name = "Corn Bread", Style = PlaceholderArtGenerator.ProductStyle.CornBread, SellPrice = 22 },
        };

        private static readonly RecipeSeed[] RecipeSeeds =
        {
            new RecipeSeed { Id = "bake_bread", ProductId = "bread", FactoryId = "bakery",
                Ingredients = new[] { ("wheat", 2) }, Seconds = 180, Xp = 3, Unlock = 1 },
            new RecipeSeed { Id = "bake_corn_bread", ProductId = "corn_bread", FactoryId = "bakery",
                Ingredients = new[] { ("corn", 2), ("wheat", 1) }, Seconds = 360, Xp = 5, Unlock = 2 },
        };

        private const int BakerySlots = 3;

        private static void CreateProduction()
        {
            PlaceholderArtGenerator.EnsureFolder(PlaceholderArtGenerator.ProductsArtFolder);
            var content = AssetDatabase.LoadAssetAtPath<GameContent>(ContentPath);

            var products = content.Products.Where(p => p != null).ToList();
            foreach (var s in ProductSeeds)
            {
                var path = $"{Root}/Data/Products/Product_{s.Id}.asset";
                var product = AssetDatabase.LoadAssetAtPath<ProductDefinition>(path);
                if (product == null)
                {
                    product = ScriptableObject.CreateInstance<ProductDefinition>();
                    product.EditorConfigure(s.Id, s.Name, PlaceholderArtGenerator.CreateProductIcon(s.Id, s.Style), s.SellPrice);
                    AssetDatabase.CreateAsset(product, path);
                }

                if (!products.Contains(product)) products.Add(product);
            }

            var recipes = content.Recipes.Where(r => r != null).ToList();
            var byFactory = new Dictionary<string, List<RecipeDefinition>>();
            foreach (var s in RecipeSeeds)
            {
                var path = $"{Root}/Data/Recipes/Recipe_{s.Id}.asset";
                var recipe = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(path);
                if (recipe == null)
                {
                    recipe = ScriptableObject.CreateInstance<RecipeDefinition>();
                    var output = products.First(p => p.Id == s.ProductId);
                    var ingredients = s.Ingredients.Select(i =>
                    {
                        content.TryGetItem(i.item, out var item);
                        if (item == null) item = products.FirstOrDefault(p => p.Id == i.item);
                        return new Ingredient { item = item, amount = i.amount };
                    }).ToList();
                    recipe.EditorConfigure(s.Id, output, 1, ingredients, s.Seconds, s.Xp, s.Unlock);
                    AssetDatabase.CreateAsset(recipe, path);
                }

                if (!recipes.Contains(recipe)) recipes.Add(recipe);
                if (!byFactory.TryGetValue(s.FactoryId, out var list)) byFactory[s.FactoryId] = list = new List<RecipeDefinition>();
                list.Add(recipe);
            }

            content.EditorSetProduction(products, recipes);
            EditorUtility.SetDirty(content);

            // Turn the seeded buildings into factories (only if a designer hasn't configured recipes already).
            foreach (var pair in byFactory)
            {
                if (!content.TryGetBuilding(pair.Key, out var factory) || factory.IsFactory) continue;
                factory.EditorConfigureFactory(pair.Value, BakerySlots);
                EditorUtility.SetDirty(factory);
            }
        }

        private static void CreateCrops()
        {
            var crops = new List<CropDefinition>();
            foreach (var s in CropSeeds)
            {
                var path = $"{Root}/Data/Crops/Crop_{s.Id}.asset";
                var crop = AssetDatabase.LoadAssetAtPath<CropDefinition>(path);
                if (crop == null)
                {
                    crop = ScriptableObject.CreateInstance<CropDefinition>();
                    crop.EditorConfigure(s.Id, s.Name, PlaceholderArtGenerator.CreateCropStages(s.Id, s.Style),
                        s.GrowSeconds, s.Yield, s.SeedCost, s.Xp, s.Unlock, s.SellPrice);
                    AssetDatabase.CreateAsset(crop, path);
                }

                crops.Add(crop);
            }

            var content = AssetDatabase.LoadAssetAtPath<GameContent>(ContentPath);
            var merged = content.Crops.Where(c => c != null).ToList();
            foreach (var c in crops)
                if (!merged.Contains(c)) merged.Add(c);
            content.EditorSetCrops(merged);
            EditorUtility.SetDirty(content);
        }

        public const string AdsConfigPath = Root + "/ScriptableObjects/AdsConfig.asset";

        /// <summary>
        /// Creates AdsConfig (test ad units), links it from GameContent, and puts Google's TEST app id into the
        /// Google Mobile Ads settings if none is set yet. Replace both with your real ids before release.
        /// </summary>
        private static void ConfigureAds()
        {
            var config = AssetDatabase.LoadAssetAtPath<AdsConfig>(AdsConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<AdsConfig>();
                AssetDatabase.CreateAsset(config, AdsConfigPath);
            }

            var content = AssetDatabase.LoadAssetAtPath<GameContent>(ContentPath);
            var so = new SerializedObject(content);
            var prop = so.FindProperty("adsConfig");
            if (prop.objectReferenceValue == null)
            {
                prop.objectReferenceValue = config;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var settingsType = Type.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings, GoogleMobileAds.Editor");
            var load = settingsType?.GetMethod("LoadInstance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (load == null)
            {
                Debug.LogWarning("[ProjectSetup] Google Mobile Ads plugin not found; set the AdMob app id manually.");
                return;
            }

            var settings = (UnityEngine.Object)load.Invoke(null, null);
            var settingsSo = new SerializedObject(settings);
            var appId = settingsSo.FindProperty("adMobAndroidAppId");
            if (appId != null && string.IsNullOrWhiteSpace(appId.stringValue))
            {
                appId.stringValue = AdsConfig.GoogleTestAppIdAndroid;
                settingsSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
                Debug.Log("[ProjectSetup] AdMob Android app id set to Google's TEST id.");
            }
        }

        private static BuildingView CreateBuildingPrefab()
        {
            var path = BuildingPrefabPath;
            var existing = AssetDatabase.LoadAssetAtPath<BuildingView>(path);
            if (existing != null) return existing;

            var go = new GameObject("BuildingView");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = SortingLayers.Objects;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            var view = go.AddComponent<BuildingView>();
            EditorUtil.Wire(view, ("spriteRenderer", sr));
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            return prefab.GetComponent<BuildingView>();
        }

        private static void ConfigureBuildScenes()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(SceneBuilder.BootPath, true),
                new EditorBuildSettingsScene(SceneBuilder.MainMenuPath, true),
                new EditorBuildSettingsScene(SceneBuilder.TownPath, true),
            };
        }
    }
}
