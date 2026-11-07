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
        public const string ApplicationId = "com.yourstudio.townbuilder";
        public const string CompanyName = "YourStudio";
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
            public int W, H, HeightPx, Unlock, Cost, MaxCount;
            public bool Removable;
            public Color32 Wall, Roof;
        }

        // First content set for Milestone 1. Balance values are placeholders; tune them in the assets afterwards.
        private static readonly BuildingSeed[] Seeds =
        {
            new BuildingSeed { Id = "town_hall", Name = "Town Hall", Category = BuildingCategory.Community, Folder = "Community",
                W = 3, H = 3, HeightPx = 150, Unlock = 1, Cost = 0, Removable = false, MaxCount = 1,
                Wall = new Color32(240, 226, 196, 255), Roof = new Color32(70, 120, 190, 255) },
            new BuildingSeed { Id = "cottage", Name = "Cottage", Category = BuildingCategory.Residential, Folder = "Residential",
                W = 2, H = 2, HeightPx = 80, Unlock = 1, Cost = 150, Removable = true,
                Wall = new Color32(250, 236, 200, 255), Roof = new Color32(214, 92, 70, 255) },
            new BuildingSeed { Id = "barn", Name = "Barn", Category = BuildingCategory.Production, Folder = "Production",
                W = 2, H = 2, HeightPx = 110, Unlock = 1, Cost = 300, Removable = true,
                Wall = new Color32(190, 72, 56, 255), Roof = new Color32(120, 84, 64, 255) },
            new BuildingSeed { Id = "bakery", Name = "Bakery", Category = BuildingCategory.Production, Folder = "Production",
                W = 2, H = 2, HeightPx = 95, Unlock = 1, Cost = 450, Removable = true,
                Wall = new Color32(246, 214, 160, 255), Roof = new Color32(226, 140, 50, 255) },
            new BuildingSeed { Id = "silo", Name = "Silo", Category = BuildingCategory.Production, Folder = "Production",
                W = 1, H = 1, HeightPx = 140, Unlock = 3, Cost = 250, Removable = true,
                Wall = new Color32(186, 196, 206, 255), Roof = new Color32(120, 136, 150, 255) },
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

            var art = PlaceholderArtGenerator.Generate(Seeds.Select(s => new PlaceholderArtGenerator.BuildingArtSpec
            {
                Id = s.Id, Folder = $"{PlaceholderArtGenerator.BuildingsArtFolder}/{s.Folder}", FootprintX = s.W, FootprintY = s.H,
                HeightPx = s.HeightPx, Wall = s.Wall, Roof = s.Roof,
            }));

            CreateContent(art);
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
                    AssetDatabase.CreateAsset(def, path);
                }
                else if (def.Sprite == null)
                {
                    def.EditorConfigure(def.Id, def.DisplayName, def.Category, art.Buildings[s.Id],
                        new Vector2Int(def.FootprintSize.X, def.FootprintSize.Y), def.UnlockLevel, def.CoinCost, def.Removable, def.MaxCount);
                    EditorUtility.SetDirty(def);
                }

                definitions.Add(def);
            }

            var contentPath = ContentPath;
            var content = AssetDatabase.LoadAssetAtPath<GameContent>(contentPath);
            if (content == null)
            {
                content = ScriptableObject.CreateInstance<GameContent>();
                var townHall = definitions.First(d => d.Id == "town_hall");
                content.EditorConfigure(definitions, new List<StarterBuilding>
                {
                    new StarterBuilding { building = townHall, origin = new Vector2Int(18, 18) },
                });
                AssetDatabase.CreateAsset(content, contentPath);
            }

            return content;
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
