using System;
using System.Collections.Generic;
using System.Linq;
using TownBuilder.Domain;
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
        /// <summary>TextMeshPro's bundled font (Liberation Sans, SIL Open Font License). Swap for the final game font later.</summary>
        public const string UIFontAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        private const string TmpEssentialsPackage = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";
        public const string UIFontOutlineMaterialPath = Root + "/Materials/UIFont Outline.mat";
        public const string SilhouetteShaderPath = Root + "/Shaders/SpriteSilhouette.shader";
        public const string SilhouetteMaterialPath = Root + "/Materials/SpriteSilhouette.mat";
        public const string SwayShaderPath = Root + "/Shaders/SpriteSway.shader";
        public const string SwayMaterialPath = Root + "/Materials/CropSway.mat";

        /// <summary>Chimney tops measured on the v0.1 AI art (0-1 from the sprite's bottom-left). Re-measure if the art changes.</summary>
        private static readonly Dictionary<string, Vector2> ChimneyPoints = new Dictionary<string, Vector2>
        {
            { "bakery", new Vector2(0.805f, 0.92f) },
            { "feed_mill", new Vector2(0.447f, 0.965f) },
            { "sugar_mill", new Vector2(0.637f, 0.965f) },
            { "creamery", new Vector2(0.655f, 0.97f) },
        };

        private static readonly string[] Folders =
        {
            "Art/Buildings/Residential", "Art/Buildings/Production", "Art/Buildings/Community", "Art/Crops",
            "Art/Environment/Tiles/TileAssets", "Art/Characters", "Art/Vehicles", "Art/Decorations", "Art/UI",
            "Animations", "Audio", "Fonts", "Materials", "Shaders",
            "Prefabs/World", "Prefabs/Buildings", "Prefabs/Crops", "Prefabs/Characters", "Prefabs/UI",
            "Scenes", "Data/Buildings", "Data/Crops", "Data/Products", "Data/Recipes", "Data/Tasks", "Data/Materials",
            "ScriptableObjects", "Settings", "Tests/EditMode", "Tests/PlayMode",
        };

        private struct BuildingSeed
        {
            public string Id, Name, Folder;
            public BuildingCategory Category;
            public int W, H, HeightPx, Unlock, Cost, MaxCount, StorageBonus, BuildSeconds, BuildXp;
            public bool Removable, FarmPlot;
            /// <summary>Landmark: fixed place in every town, not movable, not sold in the shop.</summary>
            public bool Fixed;
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
            // Milestone 4 step 1: feed ingredient.
            new CropSeed { Id = "soybean", Name = "Soybean", Style = PlaceholderArtGenerator.CropStyle.Soybean,
                GrowSeconds = 480, Yield = 3, SeedCost = 4, Xp = 2, Unlock = 4, SellPrice = 6 },
            // Milestone 4 step 3: sugar ingredient.
            new CropSeed { Id = "sugarcane", Name = "Sugarcane", Style = PlaceholderArtGenerator.CropStyle.Sugarcane,
                GrowSeconds = 720, Yield = 3, SeedCost = 5, Xp = 3, Unlock = 7, SellPrice = 8 },
        };

        /// <summary>Fixed Train Station spot (2x3 footprint). Its platform track lies along its front-left side, over the railway.</summary>
        public static readonly Vector2Int TrainStationOrigin = new Vector2Int(10, 18);
        public const int TrainStationHeight = 3;

        /// <summary>The railway: every cell of this grid column (except road crossings and water) is track. It runs along
        /// the station's front-left side, so the station's own platform track lies on top of it.</summary>
        public const int RailColumn = 9;

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
            new BuildingSeed { Id = "feed_mill", Name = "Feed Mill", Category = BuildingCategory.Production, Folder = "Production",
                W = 2, H = 2, HeightPx = 125, Unlock = 4, Cost = 600, Removable = true, BuildSeconds = 150, BuildXp = 12,
                Wall = new Color32(200, 168, 120, 255), Roof = new Color32(96, 132, 78, 255) },
            new BuildingSeed { Id = "sugar_mill", Name = "Sugar Mill", Category = BuildingCategory.Production, Folder = "Production",
                W = 2, H = 2, HeightPx = 130, Unlock = 7, Cost = 1000, Removable = true, BuildSeconds = 300, BuildXp = 20,
                Wall = new Color32(214, 200, 176, 255), Roof = new Color32(150, 70, 90, 255) },
            new BuildingSeed { Id = "creamery", Name = "Creamery", Category = BuildingCategory.Production, Folder = "Production",
                W = 2, H = 2, HeightPx = 100, Unlock = 8, Cost = 1400, Removable = true, BuildSeconds = 360, BuildXp = 24,
                Wall = new Color32(250, 244, 226, 255), Roof = new Color32(90, 150, 200, 255) },
            // Milestone 5 step 2: the train.
            new BuildingSeed { Id = "train_station", Name = "Train Station", Category = BuildingCategory.Community, Folder = "Community",
                W = 2, H = 3, HeightPx = 95, Unlock = 5, Cost = 0, Removable = false, MaxCount = 1, Fixed = true,
                Wall = new Color32(220, 196, 150, 255), Roof = new Color32(60, 110, 140, 255) },
            // Milestone 4 step 2: animal buildings (factories that turn feed into animal products).
            new BuildingSeed { Id = "chicken_coop", Name = "Chicken Coop", Category = BuildingCategory.Farming, Folder = "Farming",
                W = 2, H = 2, HeightPx = 70, Unlock = 5, Cost = 800, Removable = true, BuildSeconds = 180, BuildXp = 14,
                Wall = new Color32(236, 220, 186, 255), Roof = new Color32(176, 96, 64, 255) },
            new BuildingSeed { Id = "dairy_shed", Name = "Dairy Shed", Category = BuildingCategory.Farming, Folder = "Farming",
                W = 2, H = 2, HeightPx = 90, Unlock = 6, Cost = 1000, Removable = true, BuildSeconds = 240, BuildXp = 18,
                Wall = new Color32(246, 246, 240, 255), Roof = new Color32(70, 70, 76, 255) },
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
            EnsureTextMeshPro();
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
            PlaceholderArtGenerator.CreateLockedTile();
            PlaceholderArtGenerator.CreateRailTile();
            PlaceholderArtGenerator.CreateLockIcon();
            PlaceholderArtGenerator.CreateSmokePuff();
            PlaceholderArtGenerator.CreateTrainSprite();
            PlaceholderArtGenerator.CreatePeopleSprites();
            PlaceholderArtGenerator.CreateRewardIcons();
            PlaceholderArtGenerator.CreateCarSprite(true);
            PlaceholderArtGenerator.CreateCarSprite(false);
            PlaceholderArtGenerator.CreateSaleSign();
            var trainSprite = CreateTrainAtStationSprite();

            CreateContent(art);
            CreateCrops();
            CreateProduction();
            CreateMaterials();
            ConfigureLand();
            CreateTasks();
            ConfigureTrain(trainSprite);
            ConfigureAds();
            ConfigureAudio();
            ConfigureEffects();
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
                    if (s.Fixed) def.EditorConfigureLandmark(false, false);
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

            // The Train Station is a landmark at a fixed spot beside the railway.
            var station = definitions.First(d => d.Id == "train_station");
            if (!content.StarterBuildings.Any(s => s.building == station))
            {
                var starters = content.StarterBuildings.ToList();
                starters.Add(new StarterBuilding { building = station, origin = TrainStationOrigin });
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
            new ProductSeed { Id = "chicken_feed", Name = "Chicken Feed", Style = PlaceholderArtGenerator.ProductStyle.ChickenFeed, SellPrice = 16 },
            new ProductSeed { Id = "cow_feed", Name = "Cow Feed", Style = PlaceholderArtGenerator.ProductStyle.CowFeed, SellPrice = 34 },
            new ProductSeed { Id = "egg", Name = "Egg", Style = PlaceholderArtGenerator.ProductStyle.Egg, SellPrice = 26 },
            new ProductSeed { Id = "milk", Name = "Milk", Style = PlaceholderArtGenerator.ProductStyle.Milk, SellPrice = 52 },
            new ProductSeed { Id = "sugar", Name = "Sugar", Style = PlaceholderArtGenerator.ProductStyle.Sugar, SellPrice = 40 },
            // Milestone 4 step 4: sweets and dairy.
            new ProductSeed { Id = "cookies", Name = "Cookies", Style = PlaceholderArtGenerator.ProductStyle.Cookies, SellPrice = 62 },
            new ProductSeed { Id = "cake", Name = "Cake", Style = PlaceholderArtGenerator.ProductStyle.Cake, SellPrice = 205 },
            new ProductSeed { Id = "butter", Name = "Butter", Style = PlaceholderArtGenerator.ProductStyle.Butter, SellPrice = 72 },
            new ProductSeed { Id = "cheese", Name = "Cheese", Style = PlaceholderArtGenerator.ProductStyle.Cheese, SellPrice = 140 },
        };

        private static readonly RecipeSeed[] RecipeSeeds =
        {
            new RecipeSeed { Id = "bake_bread", ProductId = "bread", FactoryId = "bakery",
                Ingredients = new[] { ("wheat", 2) }, Seconds = 180, Xp = 3, Unlock = 1 },
            new RecipeSeed { Id = "bake_corn_bread", ProductId = "corn_bread", FactoryId = "bakery",
                Ingredients = new[] { ("corn", 2), ("wheat", 1) }, Seconds = 360, Xp = 5, Unlock = 2 },
            new RecipeSeed { Id = "make_chicken_feed", ProductId = "chicken_feed", FactoryId = "feed_mill",
                Ingredients = new[] { ("wheat", 2), ("corn", 1) }, Seconds = 240, Xp = 3, Unlock = 4 },
            new RecipeSeed { Id = "make_cow_feed", ProductId = "cow_feed", FactoryId = "feed_mill",
                Ingredients = new[] { ("corn", 2), ("soybean", 2) }, Seconds = 420, Xp = 5, Unlock = 5 },
            new RecipeSeed { Id = "collect_eggs", ProductId = "egg", FactoryId = "chicken_coop",
                Ingredients = new[] { ("chicken_feed", 1) }, Seconds = 360, Xp = 4, Unlock = 5 },
            new RecipeSeed { Id = "collect_milk", ProductId = "milk", FactoryId = "dairy_shed",
                Ingredients = new[] { ("cow_feed", 1) }, Seconds = 600, Xp = 6, Unlock = 6 },
            new RecipeSeed { Id = "make_sugar", ProductId = "sugar", FactoryId = "sugar_mill",
                Ingredients = new[] { ("sugarcane", 3) }, Seconds = 480, Xp = 5, Unlock = 7 },
            new RecipeSeed { Id = "bake_cookies", ProductId = "cookies", FactoryId = "bakery",
                Ingredients = new[] { ("wheat", 2), ("sugar", 1) }, Seconds = 600, Xp = 7, Unlock = 8 },
            new RecipeSeed { Id = "bake_cake", ProductId = "cake", FactoryId = "bakery",
                Ingredients = new[] { ("wheat", 2), ("egg", 2), ("milk", 1), ("sugar", 1) }, Seconds = 1200, Xp = 14, Unlock = 10 },
            new RecipeSeed { Id = "churn_butter", ProductId = "butter", FactoryId = "creamery",
                Ingredients = new[] { ("milk", 1) }, Seconds = 480, Xp = 6, Unlock = 8 },
            new RecipeSeed { Id = "make_cheese", ProductId = "cheese", FactoryId = "creamery",
                Ingredients = new[] { ("milk", 2) }, Seconds = 900, Xp = 10, Unlock = 9 },
        };

        private const int DefaultFactorySlots = 3;

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

            // Give each seeded factory its seed recipes. Existing factories keep their recipes, order and slot count;
            // seed recipes they don't have yet (added in a later milestone) are appended.
            foreach (var pair in byFactory)
            {
                if (!content.TryGetBuilding(pair.Key, out var factory)) continue;
                if (!factory.IsFactory)
                {
                    factory.EditorConfigureFactory(pair.Value, DefaultFactorySlots);
                    EditorUtility.SetDirty(factory);
                    continue;
                }

                var merged = factory.Recipes.Where(r => r != null).ToList();
                var missing = pair.Value.Where(r => !merged.Contains(r)).ToList();
                if (missing.Count == 0) continue;
                merged.AddRange(missing);
                factory.EditorConfigureFactory(merged, factory.ProductionSlots);
                EditorUtility.SetDirty(factory);
            }
        }

        private struct TaskSeed
        {
            public string Id, Target;
            public TaskKind Kind;
            public int Amount, Unlock, Coins, Xp, Gems;
            public bool Repeatable;
            public string Material;
            public int MaterialAmount;

            public TaskSeed(string id, TaskKind kind, string target, int amount, int unlock, int coins, int xp, int gems = 0, bool repeatable = false,
                string material = null, int materialAmount = 0)
            {
                Id = id; Kind = kind; Target = target; Amount = amount; Unlock = unlock;
                Coins = coins; Xp = xp; Gems = gems; Repeatable = repeatable;
                Material = material; MaterialAmount = materialAmount;
            }
        }

        // Milestone 5 step 1. One-off guide tasks first (shown in this order), then repeatable ones. Example values.
        private static readonly TaskSeed[] TaskSeeds =
        {
            new TaskSeed("guide_harvest_wheat", TaskKind.Harvest, "wheat", 4, 1, 20, 3),
            new TaskSeed("guide_build_bakery", TaskKind.Build, "bakery", 1, 1, 40, 5),
            new TaskSeed("guide_bake_bread", TaskKind.Collect, "bread", 2, 1, 30, 4),
            new TaskSeed("guide_deliver_orders", TaskKind.Deliver, null, 2, 1, 40, 5),
            new TaskSeed("guide_build_barn", TaskKind.Build, "barn", 1, 1, 40, 5),
            new TaskSeed("guide_harvest_corn", TaskKind.Harvest, "corn", 6, 2, 30, 4),
            new TaskSeed("guide_build_silo", TaskKind.Build, "silo", 1, 3, 40, 5),
            new TaskSeed("guide_build_feed_mill", TaskKind.Build, "feed_mill", 1, 4, 60, 8, 1),
            new TaskSeed("guide_build_chicken_coop", TaskKind.Build, "chicken_coop", 1, 5, 60, 8, 1),
            new TaskSeed("guide_collect_eggs", TaskKind.Collect, "egg", 3, 5, 60, 6),
            new TaskSeed("guide_build_dairy_shed", TaskKind.Build, "dairy_shed", 1, 6, 80, 10, 1),
            new TaskSeed("guide_build_train_station", TaskKind.Build, "train_station", 1, 6, 80, 10, 1),
            new TaskSeed("guide_build_sugar_mill", TaskKind.Build, "sugar_mill", 1, 7, 80, 10, 1),
            new TaskSeed("guide_build_creamery", TaskKind.Build, "creamery", 1, 8, 100, 12, 1),
            new TaskSeed("guide_bake_cake", TaskKind.Collect, "cake", 1, 10, 150, 15, 2),

            new TaskSeed("harvest_wheat", TaskKind.Harvest, "wheat", 10, 1, 25, 4, repeatable: true),
            new TaskSeed("bake_bread", TaskKind.Collect, "bread", 3, 1, 35, 5, repeatable: true),
            new TaskSeed("deliver_orders", TaskKind.Deliver, null, 3, 2, 60, 6, repeatable: true),
            new TaskSeed("harvest_corn", TaskKind.Harvest, "corn", 9, 2, 35, 5, repeatable: true),
            new TaskSeed("make_chicken_feed", TaskKind.Collect, "chicken_feed", 3, 4, 45, 6, repeatable: true),
            new TaskSeed("harvest_soybean", TaskKind.Harvest, "soybean", 6, 4, 40, 5, repeatable: true),
            new TaskSeed("collect_eggs", TaskKind.Collect, "egg", 4, 5, 70, 8, repeatable: true),
            new TaskSeed("collect_milk", TaskKind.Collect, "milk", 3, 6, 100, 9, repeatable: true),
            new TaskSeed("make_sugar", TaskKind.Collect, "sugar", 2, 7, 90, 9, repeatable: true),
            new TaskSeed("bake_cookies", TaskKind.Collect, "cookies", 2, 8, 120, 10, repeatable: true),
            new TaskSeed("churn_butter", TaskKind.Collect, "butter", 2, 8, 120, 10, repeatable: true),
            new TaskSeed("make_cheese", TaskKind.Collect, "cheese", 1, 9, 120, 10, repeatable: true),

            // Milestone 5 step 3: tasks that pay building materials.
            new TaskSeed("guide_load_crates", TaskKind.LoadCrates, null, 3, 5, 60, 8, material: "planks", materialAmount: 2),
            new TaskSeed("orders_for_planks", TaskKind.Deliver, null, 2, 3, 30, 4, repeatable: true, material: "planks", materialAmount: 1),
            new TaskSeed("bread_for_nails", TaskKind.Collect, "bread", 4, 3, 30, 4, repeatable: true, material: "nails", materialAmount: 1),
            new TaskSeed("corn_for_bricks", TaskKind.Harvest, "corn", 12, 4, 30, 5, repeatable: true, material: "bricks", materialAmount: 1),
            new TaskSeed("load_crates", TaskKind.LoadCrates, null, 4, 5, 60, 8, repeatable: true, material: "bricks", materialAmount: 1),

            // Milestone 5 step 4.
            new TaskSeed("guide_buy_land", TaskKind.BuyLand, null, 1, 3, 50, 6, material: "planks", materialAmount: 1),
        };

        public const string TrainAtStationSpritePath = Root + "/Art/Buildings/Community/PH_Building_train_station_train.png";

        private static Sprite CreateTrainAtStationSprite()
        {
            var s = Seeds.First(b => b.Id == "train_station");
            return PlaceholderArtGenerator.CreateBuildingWithTrain(new PlaceholderArtGenerator.BuildingArtSpec
            {
                Id = s.Id, Folder = $"{PlaceholderArtGenerator.BuildingsArtFolder}/{s.Folder}", FootprintX = s.W, FootprintY = s.H,
                HeightPx = s.HeightPx, Wall = s.Wall, Roof = s.Roof,
            }, TrainAtStationSpritePath);
        }

        /// <summary>Links the "train at the platform" sprite from GameContent (only if none is set yet).</summary>
        private static void ConfigureTrain(Sprite trainSprite)
        {
            var content = AssetDatabase.LoadAssetAtPath<GameContent>(ContentPath);
            var so = new SerializedObject(content);
            var prop = so.FindProperty("trainAtStationSprite");
            if (prop.objectReferenceValue != null) return;
            prop.objectReferenceValue = trainSprite != null ? trainSprite : AssetDatabase.LoadAssetAtPath<Sprite>(TrainAtStationSpritePath);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private struct MaterialSeed
        {
            public string Id, Name;
            public PlaceholderArtGenerator.ProductStyle Style;
            public int SellPrice;
        }

        // Milestone 5 step 3. Sell prices are low on purpose: materials are worth more when spent on land.
        private static readonly MaterialSeed[] MaterialSeeds =
        {
            new MaterialSeed { Id = "planks", Name = "Planks", Style = PlaceholderArtGenerator.ProductStyle.Planks, SellPrice = 10 },
            new MaterialSeed { Id = "bricks", Name = "Bricks", Style = PlaceholderArtGenerator.ProductStyle.Bricks, SellPrice = 14 },
            new MaterialSeed { Id = "nails", Name = "Nails", Style = PlaceholderArtGenerator.ProductStyle.Nails, SellPrice = 8 },
        };

        /// <summary>Land prices per ring (only if none are set yet). Example values; tune in GameContent → Land expansion.</summary>
        private static void ConfigureLand()
        {
            var content = AssetDatabase.LoadAssetAtPath<GameContent>(ContentPath);
            if (content.LandTiers.Count > 0) return;
            MaterialDefinition M(string id) => content.TryGetItem(id, out var item) ? item as MaterialDefinition : null;
            content.EditorSetLandTiers(new List<LandTier>
            {
                new LandTier
                {
                    unlockLevel = 3, coins = 400,
                    materials = new List<MaterialCost> { new MaterialCost { material = M("planks"), amount = 2 }, new MaterialCost { material = M("nails"), amount = 1 } },
                },
                new LandTier
                {
                    unlockLevel = 9, coins = 1200,
                    materials = new List<MaterialCost>
                    {
                        new MaterialCost { material = M("planks"), amount = 3 }, new MaterialCost { material = M("bricks"), amount = 2 },
                        new MaterialCost { material = M("nails"), amount = 2 },
                    },
                },
            });
            EditorUtility.SetDirty(content);
        }

        private static void CreateMaterials()
        {
            PlaceholderArtGenerator.EnsureFolder(PlaceholderArtGenerator.MaterialsArtFolder);
            var content = AssetDatabase.LoadAssetAtPath<GameContent>(ContentPath);
            var materials = content.Materials.Where(m => m != null).ToList();
            foreach (var s in MaterialSeeds)
            {
                var path = $"{Root}/Data/Materials/Material_{s.Id}.asset";
                var material = AssetDatabase.LoadAssetAtPath<MaterialDefinition>(path);
                if (material == null)
                {
                    material = ScriptableObject.CreateInstance<MaterialDefinition>();
                    material.EditorConfigure(s.Id, s.Name, PlaceholderArtGenerator.CreateMaterialIcon(s.Id, s.Style), s.SellPrice);
                    AssetDatabase.CreateAsset(material, path);
                }

                if (!materials.Contains(material)) materials.Add(material);
            }

            content.EditorSetMaterials(materials);
            EditorUtility.SetDirty(content);
        }

        /// <summary>Creates missing task assets and appends them to GameContent (keeps existing ones and their order).</summary>
        private static void CreateTasks()
        {
            var content = AssetDatabase.LoadAssetAtPath<GameContent>(ContentPath);
            var tasks = content.Tasks.Where(t => t != null).ToList();
            foreach (var s in TaskSeeds)
            {
                var path = $"{Root}/Data/Tasks/Task_{s.Id}.asset";
                var task = AssetDatabase.LoadAssetAtPath<TaskDefinition>(path);
                if (task == null)
                {
                    ItemDefinition item = null;
                    BuildingDefinition building = null;
                    if (s.Kind == TaskKind.Build) content.TryGetBuilding(s.Target, out building);
                    else if (s.Target != null) content.TryGetItem(s.Target, out item);
                    if (s.Target != null && item == null && building == null)
                    {
                        Debug.LogError($"[Setup] Task {s.Id}: unknown target '{s.Target}'");
                        continue;
                    }

                    task = ScriptableObject.CreateInstance<TaskDefinition>();
                    task.EditorConfigure(s.Id, s.Kind, item, building, s.Amount, s.Unlock, s.Repeatable, s.Coins, s.Xp, s.Gems);
                    if (s.Material != null && content.TryGetItem(s.Material, out var reward) && reward is MaterialDefinition material)
                        task.EditorSetMaterialReward(material, s.MaterialAmount);
                    AssetDatabase.CreateAsset(task, path);
                }

                if (!tasks.Contains(task)) tasks.Add(task);
            }

            content.EditorSetTasks(tasks);
            EditorUtility.SetDirty(content);
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

        /// <summary>Generates placeholder sounds + music (if missing) and links the SoundLibrary from GameContent.</summary>
        /// <summary>
        /// Imports TextMeshPro's essential resources (settings, shaders, default font) once. They ship inside the
        /// UI package (com.unity.ugui) already in the project, so no new package is added.
        /// </summary>
        private static void EnsureTextMeshPro()
        {
            if (AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(UIFontAssetPath) != null) return;
            string package = System.IO.Path.GetFullPath(TmpEssentialsPackage);
            if (!System.IO.File.Exists(package)) throw new InvalidOperationException($"TMP essentials not found at {package}");
            // Package import finishes after this method returns, so setup has to run again afterwards.
            // Command line alternative: Unity.exe -batchmode -quit -projectPath . -importPackage "<that package>"
            AssetDatabase.ImportPackage(package, false);
            throw new InvalidOperationException("Importing TextMeshPro essential resources now. Run setup again once the import has finished.");
        }

        /// <summary>Sets chimney points on factories that don't have one yet (keeps designer edits).</summary>
        private static void ConfigureChimneys()
        {
            var content = AssetDatabase.LoadAssetAtPath<GameContent>(ContentPath);
            foreach (var pair in ChimneyPoints)
            {
                if (!content.TryGetBuilding(pair.Key, out var def) || def.HasChimney) continue;
                def.EditorSetChimney(pair.Value);
                EditorUtility.SetDirty(def);
            }
        }

        /// <summary>Material for the selection outline (flat-colour sprite silhouette), created once.</summary>
        private static void ConfigureEffects()
        {
            CreateFontOutlineMaterial();
            ConfigureChimneys();
            if (AssetDatabase.LoadAssetAtPath<Material>(SwayMaterialPath) == null)
            {
                var sway = AssetDatabase.LoadAssetAtPath<Shader>(SwayShaderPath);
                if (sway != null) AssetDatabase.CreateAsset(new Material(sway) { name = "CropSway" }, SwayMaterialPath);
                else Debug.LogError($"[Setup] Missing shader {SwayShaderPath}");
            }

            if (AssetDatabase.LoadAssetAtPath<Material>(SilhouetteMaterialPath) != null) return;
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(SilhouetteShaderPath);
            if (shader == null)
            {
                Debug.LogError($"[Setup] Missing shader {SilhouetteShaderPath}");
                return;
            }

            AssetDatabase.CreateAsset(new Material(shader) { name = "SpriteSilhouette" }, SilhouetteMaterialPath);
        }

        /// <summary>
        /// Outline + soft shadow variant of the UI font material, for text drawn over the world ("+2 Wheat").
        /// The mobile TMP shader only draws outlines/underlays when their keywords are on, so it's a separate preset.
        /// </summary>
        private static void CreateFontOutlineMaterial()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(UIFontOutlineMaterialPath) != null) return;
            var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(UIFontAssetPath);
            if (font == null || font.material == null) return;

            var mat = new Material(font.material) { name = "UIFont Outline" };
            mat.EnableKeyword("OUTLINE_ON");
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetFloat("_FaceDilate", 0.2f);
            mat.SetFloat("_OutlineWidth", 0.25f);
            mat.SetColor("_OutlineColor", new Color(0.12f, 0.1f, 0.08f, 1f));
            mat.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.45f));
            mat.SetFloat("_UnderlayOffsetX", 0.3f);
            mat.SetFloat("_UnderlayOffsetY", -0.6f);
            mat.SetFloat("_UnderlaySoftness", 0.15f);
            AssetDatabase.CreateAsset(mat, UIFontOutlineMaterialPath);
        }

        private static void ConfigureAudio()
        {
            var library = PlaceholderAudioGenerator.CreateLibrary();
            var content = AssetDatabase.LoadAssetAtPath<GameContent>(ContentPath);
            var so = new SerializedObject(content);
            var prop = so.FindProperty("soundLibrary");
            if (prop.objectReferenceValue == null)
            {
                prop.objectReferenceValue = library;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

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
