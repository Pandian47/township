using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using static TownBuilder.EditorTools.EditorUtil;

namespace TownBuilder.EditorTools
{
    /// <summary>Creates the Boot, MainMenu and Town scenes with their full hierarchy and wired references.</summary>
    public static class SceneBuilder
    {
        public const string ScenesFolder = "Assets/_Game/Scenes";
        public static string BootPath => $"{ScenesFolder}/{SceneNames.Boot}.unity";
        public static string MainMenuPath => $"{ScenesFolder}/{SceneNames.MainMenu}.unity";
        public static string TownPath => $"{ScenesFolder}/{SceneNames.Town}.unity";

        public const int MapSize = 36;

        private static readonly Color Green = new Color(0.30f, 0.69f, 0.31f);
        private static readonly Color Red = new Color(0.86f, 0.33f, 0.29f);
        private static readonly Color Blue = new Color(0.25f, 0.55f, 0.85f);
        private static readonly Color Slate = new Color(0.36f, 0.42f, 0.50f);
        private static readonly Color PanelColor = new Color(1f, 0.98f, 0.93f, 0.97f);
        private static readonly Color PillColor = new Color(0.12f, 0.16f, 0.22f, 0.78f);
        private static readonly Color Ink = new Color(0.17f, 0.17f, 0.2f);
        private static readonly Color Dim = new Color(0f, 0f, 0f, 0.45f);

        /// <summary>
        /// Builds missing scenes (or all, when <paramref name="overwrite"/>). Assets are loaded by path AFTER each NewScene,
        /// because opening a scene unloads assets that are only held by C# references.
        /// </summary>
        public static void BuildAll(bool overwrite)
        {
            PlaceholderArtGenerator.EnsureFolder(ScenesFolder);
            if (overwrite || !File.Exists(BootPath)) BuildBoot();
            if (overwrite || !File.Exists(MainMenuPath)) BuildMainMenu();
            if (overwrite || !File.Exists(TownPath)) BuildTown();
        }

        private static T LoadRequired<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new System.InvalidOperationException($"Missing asset {path}. Run the full setup first.");
            return asset;
        }

        private static GameContent LoadContent() => LoadRequired<GameContent>(ProjectSetup.ContentPath);

        // ================================================================== Boot

        private static void BuildBoot()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var content = LoadContent();
            CreateCamera("MainCamera", null, new Color(0.55f, 0.8f, 0.95f), 5f);
            var boot = Go("Bootstrapper");
            var bootstrapper = boot.AddComponent<GameBootstrapper>();
            Wire(bootstrapper, ("content", content));
            EditorSceneManager.SaveScene(scene, BootPath);
        }

        // ================================================================== Main menu

        private static void BuildMainMenu()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var content = LoadContent();
            CreateCamera("MainCamera", null, new Color(0.55f, 0.8f, 0.95f), 5f);

            var ui = Go("UI");
            var canvas = CreateCanvas(ui.transform);
            CreateEventSystem(ui.transform);

            var background = Stretch("Background", canvas.transform);
            AddImage(background, new Color(0.62f, 0.83f, 0.6f), rounded: false);
            var safe = Stretch("SafeArea", canvas.transform);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            var title = MakeRect("Title", safe, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -90), new Vector2(1400, 170));
            Label(title, "Text", "TownBuilder", 120, TextAnchor.MiddleCenter, Ink, FontStyle.Bold);
            var subtitle = MakeRect("Subtitle", safe, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -260), new Vector2(1400, 60));
            Label(subtitle, "Text", "An original farming & town-building game (working title)", 40, TextAnchor.MiddleCenter, new Color(0.2f, 0.3f, 0.2f));

            var column = MakeRect("Buttons", safe, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -90), new Vector2(520, 420));
            VColumn(column, 28, TextAnchor.MiddleCenter);
            var continueButton = MakeButton(column, "ContinueButton", "Continue", new Vector2(480, 110), Green, 46);
            var newGameButton = MakeButton(column, "NewGameButton", "New Game", new Vector2(480, 110), Blue, 46);
            var settingsButton = MakeButton(column, "SettingsButton", "Settings", new Vector2(480, 110), Slate, 46);

            var statusRect = MakeRect("Status", safe, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 60), new Vector2(1400, 80));
            var status = Label(statusRect, "Text", "", 36, TextAnchor.MiddleCenter, new Color(0.6f, 0.1f, 0.1f));

            // Settings panel
            var settingsRoot = Stretch("SettingsPanel", canvas.transform);
            AddImage(settingsRoot, Dim, rounded: false);
            var window = MakeRect("Window", settingsRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860, 680));
            AddImage(window, PanelColor);
            VColumn(window, 24, TextAnchor.UpperCenter, 40);
            var header = Label(window, "Title", "Settings", 56, TextAnchor.MiddleCenter, Ink, FontStyle.Bold);
            Prefer(header, 760, 80);
            var music = SliderRow(window, "Music");
            var sfx = SliderRow(window, "Sound effects");
            var privacyButton = MakeButton(window, "PrivacyOptionsButton", "Privacy & ads", new Vector2(400, 90), Blue, 36);
            var closeSettings = MakeButton(window, "CloseButton", "Close", new Vector2(300, 100), Slate);

            var dialog = CreateConfirmDialog(canvas.transform);

            var controllerGo = Go("MainMenuController");
            var controller = controllerGo.AddComponent<MainMenuController>();
            Wire(controller,
                ("fallbackContent", content),
                ("continueButton", continueButton),
                ("newGameButton", newGameButton),
                ("settingsButton", settingsButton),
                ("statusText", status),
                ("confirmDialog", dialog),
                ("settingsPanel", settingsRoot.gameObject),
                ("musicSlider", music),
                ("sfxSlider", sfx),
                ("closeSettingsButton", closeSettings),
                ("privacyOptionsButton", privacyButton));

            EditorSceneManager.SaveScene(scene, MainMenuPath);
        }

        private static Slider SliderRow(RectTransform parent, string label)
        {
            var row = MakeRect($"{label}Row", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 90));
            Prefer(row, 760, 90);
            HRow(row, 30, TextAnchor.MiddleLeft);
            var text = Label(row, "Label", label, 40, TextAnchor.MiddleLeft, Ink);
            Prefer(text, 300, 80);

            var resources = new DefaultControls.Resources
            {
                standard = RoundedSprite,
                background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
                knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
            };
            var sliderGo = DefaultControls.CreateSlider(resources);
            sliderGo.name = "Slider";
            sliderGo.transform.SetParent(row, false);
            Prefer(sliderGo.transform, 420, 50);
            var slider = sliderGo.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.8f;
            return slider;
        }

        // ================================================================== Town

        private static void BuildTown()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var content = LoadContent();
            var buildingPrefab = LoadRequired<BuildingView>(ProjectSetup.BuildingPrefabPath);
            var art = PlaceholderArtGenerator.LoadExisting();

            // --- GameManagers
            var managers = Go("GameManagers");
            var sceneController = Go("TownSceneController", managers.transform).AddComponent<TownSceneController>();

            // --- World
            var world = Go("World");
            var gridGo = Go("Grid", world.transform);
            var grid = gridGo.AddComponent<Grid>();
            grid.cellLayout = GridLayout.CellLayout.Isometric;
            grid.cellSize = new Vector3(1f, 0.5f, 1f);
            grid.cellSwizzle = GridLayout.CellSwizzle.XYZ;

            var ground = CreateTilemap("GroundTilemap", gridGo.transform, 0);
            var farm = CreateTilemap("FarmTilemap", gridGo.transform, 1);
            var road = CreateTilemap("RoadTilemap", gridGo.transform, 2);
            var water = CreateTilemap("WaterTilemap", gridGo.transform, 3);
            var rail = CreateTilemap("RailTilemap", gridGo.transform, 2);
            PaintStarterMap(ground, road, water, art);
            PaintRailway(rail, road, water);

            var isoGrid = gridGo.AddComponent<IsoGrid>();
            Wire(isoGrid, ("grid", grid), ("groundTilemap", ground));
            WireArray(isoGrid, "blockingTilemaps", water, road, rail);

            var buildingsRoot = Go("Buildings", world.transform);
            Go("Crops", world.transform);
            Go("Decorations", world.transform);
            Go("Characters", world.transform);
            var lockedLand = CreateTilemap("LockedLandTilemap", gridGo.transform, -1);
            lockedLand.GetComponent<TilemapRenderer>().sortingLayerName = SortingLayers.GroundOverlay;
            var landSigns = Go("LandSigns", world.transform);
            var highlightsGo = Go("FootprintHighlights", world.transform);
            var highlighter = highlightsGo.AddComponent<FootprintHighlighter>();
            Wire(highlighter, ("grid", isoGrid), ("cellSprite", art.CellHighlight));

            var lightGo = Go("GlobalLight2D", world.transform);
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
            ApplyLightToAllSortingLayers(light);

            // --- Camera
            var rig = Go("CameraRig");
            var cam = CreateCamera("MainCamera", rig.transform, new Color(0.28f, 0.55f, 0.75f), 6f);
            var cameraController = rig.AddComponent<IsoCameraController>();

            // --- Systems
            var systems = Go("Systems");
            var input = Go("InputHandler", systems.transform).AddComponent<PointerInput>();
            var buildingController = Go("TownBuildingController", systems.transform).AddComponent<TownBuildingController>();
            var placement = Go("PlacementSystem", systems.transform).AddComponent<PlacementSystem>();

            Wire(cameraController, ("targetCamera", cam), ("pointerInput", input));
            Wire(buildingController, ("grid", isoGrid), ("buildingPrefab", buildingPrefab), ("buildingsRoot", buildingsRoot.transform));
            Wire(placement, ("worldCamera", cam), ("pointerInput", input), ("grid", isoGrid), ("buildings", buildingController),
                ("highlighter", highlighter));
            var farming = Go("FarmingController", systems.transform).AddComponent<FarmingController>();
            var production = Go("ProductionController", systems.transform).AddComponent<ProductionController>();
            var construction = Go("ConstructionController", systems.transform).AddComponent<ConstructionController>();
            var train = Go("TrainController", systems.transform).AddComponent<TrainController>();
            var streetLife = Go("StreetLife", world.transform).AddComponent<StreetLife>();
            Wire(streetLife, ("grid", isoGrid), ("roadTilemap", road),
                ("carFront", AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.CarFrontPath)),
                ("carBack", AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.CarBackPath)));
            WireArray(streetLife, "peopleSprites", Enumerable.Range(1, PlaceholderArtGenerator.PersonVariants)
                .Select(i => AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.PersonSpritePath(i))).ToArray());
            var mover = Go("TrainMover", world.transform).AddComponent<TrainMover>();
            Wire(mover, ("grid", isoGrid), ("train", train),
                ("trainSprite", AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.TrainSpritePath)));
            SetInt(mover, "railColumn", ProjectSetup.RailColumn);
            SetFloat(mover, "stopY", ProjectSetup.TrainStationOrigin.y + (ProjectSetup.TrainStationHeight - PlaceholderArtGenerator.TrainLengthCells) * 0.5f);
            var land = Go("LandController", systems.transform).AddComponent<LandController>();
            Wire(land, ("grid", isoGrid), ("placement", placement), ("lockedTilemap", lockedLand), ("signsRoot", landSigns.transform),
                ("lockedTile", AssetDatabase.LoadAssetAtPath<TileBase>(PlaceholderArtGenerator.TileAssetPath(PlaceholderArtGenerator.LockedTileName))),
                ("saleSign", AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.SaleSignPath)));
            Wire(train, ("buildings", buildingController),
                ("lockIcon", AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.LockIconPath)));
            Wire(construction, ("buildings", buildingController), ("placement", placement),
                ("constructionIcon", AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.ConstructionIconPath)));
            Wire(production, ("buildings", buildingController), ("placement", placement));
            Wire(farming, ("buildings", buildingController), ("placement", placement),
                ("cropMaterial", AssetDatabase.LoadAssetAtPath<Material>(ProjectSetup.SwayMaterialPath)));
            var smoke = Go("ChimneySmoke", world.transform).AddComponent<ChimneySmoke>();
            Wire(smoke, ("buildings", buildingController), ("production", production),
                ("puffSprite", AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.SmokePuffPath)));
            var effectsGo = Go("TownEffects", systems.transform);
            var effects = effectsGo.AddComponent<TownEffects>();
            var outline = effectsGo.AddComponent<SelectionOutline>();
            Wire(outline, ("silhouetteMaterial", AssetDatabase.LoadAssetAtPath<Material>(ProjectSetup.SilhouetteMaterialPath)));

            // --- UI
            var uiRoot = Go("UI");
            var canvas = CreateCanvas(uiRoot.transform);
            CreateEventSystem(uiRoot.transform);
            var floatingText = BuildFloatingTextLayer(canvas, cam);
            var hud = BuildTownHud(canvas);
            Wire(effects,
                ("buildings", buildingController),
                ("placement", placement),
                ("farming", farming),
                ("production", production),
                ("construction", construction),
                ("train", train),
                ("selectionOutline", outline),
                ("floatingText", floatingText));

            // Audio: music and sound effects are played by the persistent GameAudio on GameRoot (see GameAudio.cs).

            Wire(sceneController,
                ("fallbackContent", content),
                ("grid", isoGrid),
                ("cameraController", cameraController),
                ("buildings", buildingController),
                ("placement", placement),
                ("farming", farming),
                ("production", production),
                ("construction", construction),
                ("hud", hud),
                ("effects", effects),
                ("trainController", train),
                ("landController", land),
                ("trainMover", mover),
                ("streetLife", streetLife));

            EditorSceneManager.SaveScene(scene, TownPath);
        }

        /// <summary>
        /// The fixed railway: one grid column across the whole map, along the Train Station's front-left side. Skips road
        /// crossings, water and the station itself. Rail cells block building.
        /// </summary>
        private static void SetInt(Object target, string field, int value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void PaintRailway(Tilemap rail, Tilemap road, Tilemap water)
        {
            var tile = AssetDatabase.LoadAssetAtPath<TileBase>(PlaceholderArtGenerator.TileAssetPath(PlaceholderArtGenerator.RailTileName));
            int x = ProjectSetup.RailColumn;
            var station = ProjectSetup.TrainStationOrigin;
            var cells = new List<Vector3Int>();
            for (int y = 0; y < MapSize; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                bool underStation = x >= station.x && x < station.x + 2 && y >= station.y && y < station.y + ProjectSetup.TrainStationHeight;
                if (underStation || road.HasTile(cell) || water.HasTile(cell)) continue;
                cells.Add(cell);
            }

            rail.SetTiles(cells.ToArray(), Enumerable.Repeat(tile, cells.Count).ToArray());
            rail.RefreshAllTiles();
            rail.CompressBounds();
            EditorUtility.SetDirty(rail);
        }

        private static void PaintStarterMap(Tilemap ground, Tilemap road, Tilemap water, PlaceholderArtGenerator.Result art)
        {
            // Small starter island. Designers can repaint it freely with the Tile Palette; the game reads it at runtime.
            var groundCells = new List<Vector3Int>();
            var groundTiles = new List<TileBase>();
            var roadCells = new List<Vector3Int>();
            var waterCells = new List<Vector3Int>();

            var lakeCenter = new Vector2(27f, 8f);
            for (int x = 0; x < MapSize; x++)
            {
                for (int y = 0; y < MapSize; y++)
                {
                    var cell = new Vector3Int(x, y, 0);
                    float lake = Mathf.Pow((x - lakeCenter.x) / 4.5f, 2) + Mathf.Pow((y - lakeCenter.y) / 3.5f, 2);
                    if (lake <= 1f)
                    {
                        waterCells.Add(cell);
                        continue;
                    }

                    groundCells.Add(cell);
                    groundTiles.Add(lake <= 1.7f ? art.Sand : art.Grass);

                    bool isRoad = (y == 16 && x <= 30) || (x == 16 && y >= 16);
                    if (isRoad) roadCells.Add(cell);
                }
            }

            ground.SetTiles(groundCells.ToArray(), groundTiles.ToArray());
            road.SetTiles(roadCells.ToArray(), Enumerable.Repeat<TileBase>(art.Road, roadCells.Count).ToArray());
            water.SetTiles(waterCells.ToArray(), Enumerable.Repeat<TileBase>(art.Water, waterCells.Count).ToArray());

            foreach (var map in new[] { ground, road, water })
            {
                map.RefreshAllTiles();
                map.CompressBounds();
                EditorUtility.SetDirty(map);
            }

            Debug.Log($"[SceneBuilder] Painted ground={ground.GetUsedTilesCount()} types, cells {ground.cellBounds.size}, " +
                      $"water cells={waterCells.Count}, road cells={roadCells.Count}, ground has (0,0): {ground.HasTile(Vector3Int.zero)}");
        }

        private static Tilemap CreateTilemap(string name, Transform parent, int order)
        {
            var go = Go(name, parent);
            var tilemap = go.AddComponent<Tilemap>();
            var renderer = go.AddComponent<TilemapRenderer>();
            renderer.sortingLayerName = SortingLayers.Ground;
            renderer.sortingOrder = order;
            renderer.mode = TilemapRenderer.Mode.Chunk;
            return tilemap;
        }

        private static void ApplyLightToAllSortingLayers(Light2D light)
        {
            var so = new SerializedObject(light);
            var prop = so.FindProperty("m_ApplyToSortingLayers");
            if (prop == null) return;
            var layers = SortingLayer.layers;
            prop.arraySize = layers.Length;
            for (int i = 0; i < layers.Length; i++) prop.GetArrayElementAtIndex(i).intValue = layers[i].id;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static TownHudController BuildTownHud(Canvas canvas)
        {
            var safe = Stretch("SafeArea", canvas.transform);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            // --- Top bar
            var topBarRect = MakeRect("TopBar", safe, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(-60, 90));
            HRow(topBarRect, 24, TextAnchor.MiddleLeft);
            var levelText = Pill(topBarRect, "LevelPill", 330);
            var coinsText = Pill(topBarRect, "CoinsPill", 340);
            var gemsText = Pill(topBarRect, "GemsPill", 240);
            var barnText = Pill(topBarRect, "BarnPill", 300);
            var barnPill = barnText.transform.parent.gameObject;
            var barnPillButton = barnPill.AddComponent<Button>();
            barnPillButton.targetGraphic = barnPill.GetComponent<Image>();
            var topBar = topBarRect.gameObject.AddComponent<TopBarView>();
            Wire(topBar, ("levelText", levelText), ("coinsText", coinsText), ("gemsText", gemsText), ("barnText", barnText));

            // --- Bottom navigation
            var nav = MakeRect("BottomNavigation", safe, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30, 30), new Vector2(1560, 130));
            HRow(nav, 24, TextAnchor.LowerRight);
            var adButton = MakeButton(nav, "RewardedAdButton", "Watch ad\n+100 coins", new Vector2(300, 120), new Color(0.95f, 0.65f, 0.15f), out var adLabel, 34);
            var rewarded = adButton.gameObject.AddComponent<RewardedCoinsButton>();
            Wire(rewarded, ("button", adButton), ("label", adLabel));
            var goalsButton = MakeButton(nav, "GoalsButton", "Goals", new Vector2(200, 120), new Color(0.13f, 0.58f, 0.6f));
            var goalsBadge = MakeRect("Badge", (RectTransform)goalsButton.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(-14, -10), new Vector2(54, 54));
            AddImage(goalsBadge, Red, raycast: false);
            var goalsBadgeText = Label(goalsBadge, "Text", "1", 30, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            var ordersButton = MakeButton(nav, "OrdersButton", "Orders", new Vector2(220, 120), new Color(0.55f, 0.35f, 0.7f));
            var barnButton = MakeButton(nav, "BarnButton", "Barn", new Vector2(200, 120), new Color(0.62f, 0.38f, 0.25f));
            var menuButton = MakeButton(nav, "MenuButton", "Menu", new Vector2(200, 120), Slate);
            var buildButton = MakeButton(nav, "BuildButton", "Build", new Vector2(260, 120), Green, 48);

            // --- Placement bar
            var placementBar = MakeRect("PlacementActionBar", safe, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(1100, 160));
            AddImage(placementBar, PanelColor);
            HRow(placementBar, 24, TextAnchor.MiddleCenter, 24);
            var placementInfo = Label(placementBar, "Info", "", 34, TextAnchor.MiddleLeft, Ink);
            Prefer(placementInfo, 640, 120);
            var cancelPlacement = MakeButton(placementBar, "CancelButton", "Cancel", new Vector2(180, 110), Red);
            var confirmPlacement = MakeButton(placementBar, "ConfirmButton", "Place", new Vector2(180, 110), Green);

            // --- Selection bar
            var selectionBar = MakeRect("SelectionActionBar", safe, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(1100, 150));
            AddImage(selectionBar, PanelColor);
            HRow(selectionBar, 24, TextAnchor.MiddleCenter, 22);
            var selectionName = Label(selectionBar, "Name", "", 42, TextAnchor.MiddleLeft, Ink, FontStyle.Bold);
            Prefer(selectionName, 420, 100);
            var moveButton = MakeButton(selectionBar, "MoveButton", "Move", new Vector2(180, 100), Blue);
            var removeButton = MakeButton(selectionBar, "RemoveButton", "Remove", new Vector2(190, 100), Red);
            var deselectButton = MakeButton(selectionBar, "CloseButton", "Close", new Vector2(170, 100), Slate);

            // --- Town tasks (top-left, under the top bar)
            var tasksPanel = BuildTasksPanel(safe);

            // --- Farming: field panel (above the selection bar) and toast
            var fieldPanel = BuildFieldPanel(safe);
            var factoryPanel = BuildFactoryPanel(safe);
            var trainPanel = BuildTrainPanel(safe);
            var landPanel = BuildLandPanel(safe);
            var constructionPanel = BuildConstructionPanel(safe);
            var toast = BuildToast(safe);

            // --- Barn screen
            var barnPanel = BuildBarnPanel(canvas.transform);
            var ordersPanel = BuildOrdersPanel(canvas.transform);

            // --- Shop
            var shop = BuildShop(canvas.transform);

            // --- Confirm dialog (last = drawn on top)
            var dialog = CreateConfirmDialog(canvas.transform);
            var levelUp = BuildLevelUpDialog(canvas.transform);
            var achievements = BuildAchievementsPanel(canvas.transform, goalsButton, goalsBadge.gameObject, goalsBadgeText);
            var daily = BuildDailyRewardDialog(canvas.transform);
            var rewardLayer = Stretch("RewardFlyLayer", canvas.transform);
            var rewardFlyer = rewardLayer.gameObject.AddComponent<RewardFlyer>();
            Wire(rewardFlyer, ("coinsTarget", (RectTransform)coinsText.transform.parent), ("gemsTarget", (RectTransform)gemsText.transform.parent),
                ("xpTarget", (RectTransform)levelText.transform.parent),
                ("coinSprite", AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.CoinIconPath)),
                ("gemSprite", AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.GemIconPath)),
                ("xpSprite", AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.XpIconPath)));
            var devPanel = BuildDevPanel(canvas.transform);

            // Every panel and action bar fades/slides in when shown.
            foreach (var t in canvas.GetComponentsInChildren<Transform>(true))
                if (t.name == "Root" || t.name == "SelectionActionBar" || t.name == "PlacementActionBar")
                    t.gameObject.AddComponent<PanelTransition>();

            var hud = canvas.gameObject.AddComponent<TownHudController>();
            Wire(hud,
                ("fieldPanel", fieldPanel),
                ("factoryPanel", factoryPanel),
                ("trainPanel", trainPanel),
                ("landPanel", landPanel),
                ("constructionPanel", constructionPanel),
                ("barnPanel", barnPanel),
                ("ordersPanel", ordersPanel),
                ("ordersButton", ordersButton),
                ("levelUpDialog", levelUp),
                ("barnButton", barnButton),
                ("barnPillButton", barnPillButton),
                ("toast", toast),
                ("topBar", topBar),
                ("shopPanel", shop),
                ("confirmDialog", dialog),
                ("tasksPanel", tasksPanel),
                ("devPanel", devPanel),
                ("dailyDialog", daily),
                ("achievementsPanel", achievements),
                ("rewardFlyer", rewardFlyer),
                ("bottomNavigation", nav.gameObject),
                ("buildButton", buildButton),
                ("rewardedCoinsButton", rewarded),
                ("menuButton", menuButton),
                ("placementBar", placementBar.gameObject),
                ("placementInfoText", placementInfo),
                ("confirmPlacementButton", confirmPlacement),
                ("cancelPlacementButton", cancelPlacement),
                ("selectionBar", selectionBar.gameObject),
                ("selectionNameText", selectionName),
                ("moveButton", moveButton),
                ("removeButton", removeButton),
                ("deselectButton", deselectButton));
            return hud;
        }

        private static OrdersPanel BuildOrdersPanel(Transform canvas)
        {
            var container = Stretch("OrdersPanel", canvas);
            var root = Stretch("Root", container);
            AddImage(root, Dim, rounded: false);

            var window = MakeRect("Window", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1640, 820));
            AddImage(window, PanelColor);
            var titleRect = MakeRect("Title", window, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -18), new Vector2(-80, 80));
            Label(titleRect, "Text", "Orders", 54, TextAnchor.MiddleLeft, Ink, FontStyle.Bold);
            var subRect = MakeRect("Subtitle", window, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -92), new Vector2(-80, 44));
            Label(subRect, "Text", "Deliver goods for more coins than selling them, plus XP to level up.", 30, TextAnchor.MiddleLeft,
                new Color(0.35f, 0.35f, 0.35f));

            var close = MakeButton(window, "CloseButton", "X", new Vector2(90, 90), Red, 44);
            var closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-20, -16);

            var row = MakeRect("Cards", window, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            row.offsetMin = new Vector2(30, 30);
            row.offsetMax = new Vector2(-30, -150);
            HRow(row, 30, TextAnchor.MiddleCenter);

            var cards = new OrderCardView[3];
            for (int c = 0; c < cards.Length; c++)
            {
                var card = MakeRect($"Order{c + 1}", row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500, 620));
                AddImage(card, Color.white);
                Prefer(card, 500, 620);

                var icons = new Image[3];
                var texts = new TMP_Text[3];
                for (int r = 0; r < 3; r++)
                {
                    var line = MakeRect($"Line{r + 1}", card, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -24 - r * 108), new Vector2(-40, 96));
                    var iconRect = MakeRect("Icon", line, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(96, 96));
                    icons[r] = iconRect.gameObject.AddComponent<Image>();
                    icons[r].raycastTarget = false;
                    var textRect = MakeRect("Text", line, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), new Vector2(112, 0), new Vector2(-112, 0));
                    texts[r] = AddText(textRect.gameObject, "", 34, TextAnchor.MiddleLeft, Ink, FontStyle.Bold);
                }

                var rewardRect = MakeRect("Reward", card, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 140), new Vector2(-40, 60));
                var reward = Label(rewardRect, "Text", "+0 coins", 34, TextAnchor.MiddleCenter, Ink, FontStyle.Bold);

                var waitRect = MakeRect("Waiting", card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(440, 160));
                var waiting = Label(waitRect, "Text", "New order in", 38, TextAnchor.MiddleCenter, new Color(0.45f, 0.45f, 0.45f));

                var buttons = MakeRect("Buttons", card, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 20), new Vector2(-40, 110));
                HRow(buttons, 16, TextAnchor.MiddleCenter);
                var skip = MakeButton(buttons, "SkipButton", "Skip", new Vector2(140, 100), Slate, 32);
                var deliver = MakeButton(buttons, "DeliverButton", "Deliver", new Vector2(260, 100), Green, 38);

                var view = card.gameObject.AddComponent<OrderCardView>();
                Wire(view, ("rewardText", reward), ("waitingText", waiting), ("deliverButton", deliver), ("skipButton", skip));
                WireArray(view, "rowIcons", icons);
                WireArray(view, "rowTexts", texts);
                cards[c] = view;
            }

            var panel = container.gameObject.AddComponent<OrdersPanel>();
            Wire(panel, ("panelRoot", root.gameObject), ("closeButton", close));
            WireArray(panel, "cards", cards);
            root.gameObject.SetActive(false);
            return panel;
        }

        private static AchievementsPanel BuildAchievementsPanel(Transform canvas, Button openButton, GameObject badge, TMP_Text badgeText)
        {
            const int rowCount = 7;
            const float rowHeight = 112f, gap = 10f;
            var container = Stretch("AchievementsPanel", canvas);
            var root = Stretch("Root", container);
            AddImage(root, Dim, rounded: false);
            var window = MakeRect("Window", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(1500, 130 + rowCount * (rowHeight + gap) + 20));
            AddImage(window, PanelColor);

            Label(MakeRect("Title", window, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -18), new Vector2(-80, 80)),
                "Text", "Goals", 54, TextAnchor.MiddleLeft, Ink, FontStyle.Bold);
            Label(MakeRect("Subtitle", window, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(200, -18), new Vector2(-480, 80)),
                "Text", "Long-term goals. Each tier pays gems.", 30, TextAnchor.MiddleLeft, new Color(0.45f, 0.42f, 0.38f));
            var close = MakeButton(window, "CloseButton", "X", new Vector2(90, 90), Red, 44);
            var closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-20, -16);

            var rows = new AchievementRowView[rowCount];
            for (int i = 0; i < rowCount; i++)
            {
                var row = MakeRect($"Achievement{i + 1}", window, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0, -120 - i * (rowHeight + gap)), new Vector2(-60, rowHeight));
                var back = AddImage(row, Color.white, raycast: false);

                var name = Label(MakeRect("Name", row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20, -8), new Vector2(420, 50)),
                    "Text", "Name", 34, TextAnchor.MiddleLeft, Ink, FontStyle.Bold);
                var tier = Label(MakeRect("Tier", row, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20, 10), new Vector2(420, 40)),
                    "Text", "Tier 1 of 3", 26, TextAnchor.MiddleLeft, new Color(0.45f, 0.42f, 0.38f));
                var goal = Label(MakeRect("Goal", row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(450, -8), new Vector2(560, 50)),
                    "Text", "Goal", 30, TextAnchor.MiddleLeft, Ink);

                var barBg = MakeRect("Bar", row, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(450, 22), new Vector2(380, 20));
                AddImage(barBg, new Color(0.82f, 0.78f, 0.7f), raycast: false);
                var fill = AddImage(Stretch("Fill", barBg, 2f), Green, raycast: false);
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                var progress = Label(MakeRect("Progress", row, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(845, 12), new Vector2(200, 40)),
                    "Text", "0/10", 26, TextAnchor.MiddleLeft, Ink, FontStyle.Bold);

                var reward = Label(MakeRect("Reward", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-210, 0), new Vector2(170, 50)),
                    "Text", "+2 gems", 30, TextAnchor.MiddleRight, new Color(0.2f, 0.45f, 0.75f), FontStyle.Bold);
                var claim = MakeButton(row, "ClaimButton", "Claim", new Vector2(170, 80), Green, 34);
                var claimRect = (RectTransform)claim.transform;
                claimRect.anchorMin = claimRect.anchorMax = claimRect.pivot = new Vector2(1f, 0.5f);
                claimRect.anchoredPosition = new Vector2(-20, 0);

                var view = row.gameObject.AddComponent<AchievementRowView>();
                Wire(view, ("background", back), ("nameText", name), ("goalText", goal), ("tierText", tier), ("progressText", progress),
                    ("rewardText", reward), ("progressFill", fill), ("claimButton", claim));
                rows[i] = view;
            }

            var panel = container.gameObject.AddComponent<AchievementsPanel>();
            Wire(panel, ("panelRoot", root.gameObject), ("openButton", openButton), ("closeButton", close), ("badge", badge), ("badgeText", badgeText));
            WireArray(panel, "rows", rows);
            badge.SetActive(false);
            root.gameObject.SetActive(false);
            return panel;
        }

        private static DailyRewardDialog BuildDailyRewardDialog(Transform canvas)
        {
            var container = Stretch("DailyRewardDialog", canvas);
            var root = Stretch("Root", container);
            AddImage(root, Dim, rounded: false);
            var window = MakeRect("Window", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1560, 720));
            AddImage(window, new Color(1f, 0.95f, 0.75f));

            Label(MakeRect("Title", window, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -24), new Vector2(-60, 90)),
                "Text", "Daily reward", 70, TextAnchor.MiddleCenter, new Color(0.85f, 0.45f, 0.1f), FontStyle.Bold);
            Label(MakeRect("Subtitle", window, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -114), new Vector2(-60, 50)),
                "Text", "Come back every day for a bigger reward. Missing a day starts over at day 1.", 32, TextAnchor.MiddleCenter, Ink);

            var row = MakeRect("Days", window, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -180), new Vector2(1460, 300));
            HRow(row, 14, TextAnchor.MiddleCenter);
            var backs = new Image[7];
            var days = new TMP_Text[7];
            var icons = new Image[7];
            var amounts = new TMP_Text[7];
            for (int i = 0; i < 7; i++)
            {
                var card = MakeRect($"Day{i + 1}", row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(196, 300));
                Prefer(card, 196, 300);
                backs[i] = AddImage(card, Color.white, raycast: false);
                days[i] = Label(MakeRect("Day", card, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -8), new Vector2(-10, 44)),
                    "Text", $"Day {i + 1}", 28, TextAnchor.MiddleCenter, Ink, FontStyle.Bold);
                var iconRect = MakeRect("Icon", card, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(110, 110));
                icons[i] = iconRect.gameObject.AddComponent<Image>();
                icons[i].preserveAspect = true;
                icons[i].raycastTarget = false;
                amounts[i] = Label(MakeRect("Amount", card, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 12), new Vector2(-10, 110)),
                    "Text", "", 25, TextAnchor.MiddleCenter, Ink, FontStyle.Bold);
            }

            var claim = MakeButton(window, "ClaimButton", "Claim", new Vector2(460, 120), Green, out var claimLabel, 44);
            var claimRect = (RectTransform)claim.transform;
            claimRect.anchorMin = claimRect.anchorMax = claimRect.pivot = new Vector2(0.5f, 0f);
            claimRect.anchoredPosition = new Vector2(0, 50);
            var close = MakeButton(window, "CloseButton", "X", new Vector2(90, 90), Red, 44);
            var closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-20, -20);

            var dialog = container.gameObject.AddComponent<DailyRewardDialog>();
            Wire(dialog, ("dialogRoot", root.gameObject), ("claimButton", claim), ("claimLabel", claimLabel), ("closeButton", close),
                ("coinSprite", AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.CoinIconPath)),
                ("gemSprite", AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.GemIconPath)));
            WireArray(dialog, "cardBackgrounds", backs);
            WireArray(dialog, "dayTexts", days);
            WireArray(dialog, "icons", icons);
            WireArray(dialog, "amountTexts", amounts);
            root.gameObject.SetActive(false);
            return dialog;
        }

        private static LevelUpDialog BuildLevelUpDialog(Transform canvas)
        {
            var container = Stretch("LevelUpDialog", canvas);
            var root = Stretch("Root", container);
            AddImage(root, Dim, rounded: false);
            var window = MakeRect("Window", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 560));
            AddImage(window, new Color(1f, 0.95f, 0.75f));

            var titleRect = MakeRect("Title", window, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -40), new Vector2(-60, 130));
            var title = Label(titleRect, "Text", "Level 2!", 96, TextAnchor.MiddleCenter, new Color(0.85f, 0.45f, 0.1f), FontStyle.Bold);
            var rewardsRect = MakeRect("Rewards", window, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -190), new Vector2(-60, 70));
            var rewards = Label(rewardsRect, "Text", "+100 coins", 44, TextAnchor.MiddleCenter, Ink, FontStyle.Bold);
            var unlocksRect = MakeRect("Unlocks", window, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -270), new Vector2(-80, 120));
            var unlocks = Label(unlocksRect, "Text", "New: Corn", 38, TextAnchor.MiddleCenter, Ink);

            var ok = MakeButton(window, "OkButton", "Great!", new Vector2(320, 110), Green, 44);
            var okRect = (RectTransform)ok.transform;
            okRect.anchorMin = okRect.anchorMax = okRect.pivot = new Vector2(0.5f, 0f);
            okRect.anchoredPosition = new Vector2(0, 40);

            var dialog = container.gameObject.AddComponent<LevelUpDialog>();
            Wire(dialog, ("dialogRoot", root.gameObject), ("titleText", title), ("rewardsText", rewards), ("unlocksText", unlocks), ("okButton", ok));
            root.gameObject.SetActive(false);
            return dialog;
        }

        private static BarnPanel BuildBarnPanel(Transform canvas)
        {
            var container = Stretch("BarnPanel", canvas);
            var root = Stretch("Root", container);
            AddImage(root, Dim, rounded: false);

            var window = MakeRect("Window", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1640, 820));
            AddImage(window, PanelColor);

            var titleRect = MakeRect("Title", window, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -18), new Vector2(-80, 80));
            var title = Label(titleRect, "Text", "Barn", 54, TextAnchor.MiddleLeft, Ink, FontStyle.Bold);

            var close = MakeButton(window, "CloseButton", "X", new Vector2(90, 90), Red, 44);
            var closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-20, -16);

            // Items grid
            var items = MakeRect("Items", window, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            items.offsetMin = new Vector2(30, 170);
            items.offsetMax = new Vector2(-30, -120);
            var grid = items.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(360, 250);
            grid.spacing = new Vector2(20, 20);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.childAlignment = TextAnchor.UpperLeft;

            var template = MakeRect("ItemTemplate", items, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(360, 250));
            AddImage(template, Color.white);
            var iconRect = MakeRect("Icon", template, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12, -10), new Vector2(110, 100));
            var icon = iconRect.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var nameRect = MakeRect("Name", template, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(124, -14), new Vector2(-134, 50));
            var nameText = Label(nameRect, "Text", "Item x0", 32, TextAnchor.MiddleLeft, Ink, FontStyle.Bold);
            var priceRect = MakeRect("Price", template, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(124, -62), new Vector2(-134, 62));
            var priceText = Label(priceRect, "Text", "0 coins each", 24, TextAnchor.MiddleLeft, Ink);

            var buttons = MakeRect("Buttons", template, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 12), new Vector2(-24, 110));
            HRow(buttons, 12, TextAnchor.MiddleCenter);
            var sellOne = MakeButton(buttons, "SellOneButton", "Sell 1", new Vector2(140, 100), Blue, 30);
            var sellAll = MakeButton(buttons, "SellAllButton", "Sell all", new Vector2(180, 100), Green, out var sellAllLabel, 28);

            var itemView = template.gameObject.AddComponent<BarnItemView>();
            Wire(itemView, ("icon", icon), ("nameText", nameText), ("priceText", priceText), ("sellOneButton", sellOne),
                ("sellAllButton", sellAll), ("sellAllLabel", sellAllLabel));
            template.gameObject.SetActive(false);

            var emptyRect = MakeRect("Empty", window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1200, 120));
            var empty = Label(emptyRect, "Text", "Your barn is empty.\nHarvest crops to fill it.", 40, TextAnchor.MiddleCenter, new Color(0.4f, 0.4f, 0.4f));

            // Footer: expansion
            var expand = MakeButton(window, "ExpandButton", "Expand", new Vector2(420, 120), new Color(0.62f, 0.38f, 0.25f), out var expandLabel, 34);
            var expandRect = (RectTransform)expand.transform;
            expandRect.anchorMin = expandRect.anchorMax = expandRect.pivot = new Vector2(1f, 0f);
            expandRect.anchoredPosition = new Vector2(-30, 26);
            var hintRect = MakeRect("Hint", window, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(30, 26), new Vector2(-520, 120));
            Label(hintRect, "Text", "Sell crops for coins. Build a Barn (+50) or Silo (+30), or expand here for more space.",
                30, TextAnchor.MiddleLeft, new Color(0.35f, 0.35f, 0.35f));

            var panel = container.gameObject.AddComponent<BarnPanel>();
            Wire(panel, ("panelRoot", root.gameObject), ("titleText", title), ("itemsParent", items), ("itemTemplate", itemView),
                ("emptyText", empty), ("expandButton", expand), ("expandLabel", expandLabel), ("closeButton", close));
            root.gameObject.SetActive(false);
            return panel;
        }

        private static ConstructionPanel BuildConstructionPanel(RectTransform safe)
        {
            var container = Stretch("ConstructionPanel", safe);
            var root = MakeRect("Root", container, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 196), new Vector2(1100, 220));
            AddImage(root, PanelColor);

            var titleRect = MakeRect("Title", root, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -10), new Vector2(-48, 54));
            var title = Label(titleRect, "Text", "Building: under construction", 36, TextAnchor.MiddleLeft, Ink, FontStyle.Bold);
            var statusRect = MakeRect("Status", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24, -72), new Vector2(440, 56));
            var status = Label(statusRect, "Text", "Ready in 1m 00s", 34, TextAnchor.MiddleLeft, Ink);
            var barBg = MakeRect("ProgressBar", root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24, 34), new Vector2(440, 36));
            AddImage(barBg, new Color(0.82f, 0.78f, 0.7f), raycast: false);
            var fillRect = Stretch("Fill", barBg, 4f);
            var fill = AddImage(fillRect, new Color(0.95f, 0.65f, 0.15f), raycast: false);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0.4f;

            var buttons = MakeRect("Buttons", root, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24, 20), new Vector2(580, 130));
            HRow(buttons, 16, TextAnchor.MiddleRight);
            var ad = MakeButton(buttons, "AdButton", "Finish now", new Vector2(280, 120), new Color(0.95f, 0.65f, 0.15f), out var adLabel, 30);
            var gems = MakeButton(buttons, "GemsButton", "Finish now", new Vector2(280, 120), new Color(0.3f, 0.6f, 0.85f), out var gemsLabel, 30);

            var panel = container.gameObject.AddComponent<ConstructionPanel>();
            Wire(panel, ("panelRoot", root.gameObject), ("titleText", title), ("statusText", status), ("progressFill", fill),
                ("adButton", ad), ("adLabel", adLabel), ("gemsButton", gems), ("gemsLabel", gemsLabel));
            root.gameObject.SetActive(false);
            return panel;
        }

        private static FactoryPanel BuildFactoryPanel(RectTransform safe)
        {
            var container = Stretch("FactoryPanel", safe);
            var root = MakeRect("Root", container, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 196), new Vector2(1640, 330));
            AddImage(root, PanelColor);

            var titleRect = MakeRect("Title", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24, -10), new Vector2(600, 54));
            var title = Label(titleRect, "Text", "Bakery", 38, TextAnchor.MiddleLeft, Ink, FontStyle.Bold);
            var queueTitleRect = MakeRect("QueueTitle", root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24, -10), new Vector2(772, 54));
            var queueTitle = Label(queueTitleRect, "Text", "Queue 0/3", 32, TextAnchor.MiddleLeft, Ink, FontStyle.Bold);

            // Recipes (left), scrolls sideways when a factory has more than 3
            var recipesViewport = MakeRect("RecipesViewport", root, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(960, 0));
            recipesViewport.offsetMin = new Vector2(24, 16);
            recipesViewport.offsetMax = new Vector2(824, -70);
            var recipes = HorizontalScrollList(recipesViewport, "Recipes", 16);

            var template = MakeRect("RecipeTemplate", recipes, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 240));
            var bg = AddImage(template, Color.white);
            Prefer(template, 300, 240);
            var button = template.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            var iconRect = MakeRect("Icon", template, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -6), new Vector2(-20, 100));
            var icon = iconRect.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var nameRect = MakeRect("Name", template, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 84), new Vector2(-12, 44));
            var nameText = Label(nameRect, "Text", "Product", 30, TextAnchor.MiddleCenter, Ink, FontStyle.Bold);
            var ingRect = MakeRect("Ingredients", template, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 6), new Vector2(-12, 76));
            var ingText = Label(ingRect, "Text", "", 24, TextAnchor.MiddleCenter, Ink);
            var recipeView = template.gameObject.AddComponent<RecipeButtonView>();
            Wire(recipeView, ("button", button), ("icon", icon), ("nameText", nameText), ("ingredientsText", ingText));
            template.gameObject.SetActive(false);

            // Queue (right): slots + buttons
            var queue = MakeRect("Queue", root, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
            queue.offsetMin = new Vector2(-796, 16);
            queue.offsetMax = new Vector2(-24, -70);
            var slotsRow = MakeRect("Slots", queue, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0, 140));
            HRow(slotsRow, 8, TextAnchor.UpperLeft);

            // Base 3 slots + up to 3 bought ones (GameContent.maxExtraQueueSlots).
            var slotViews = new QueueSlotView[6];
            for (int i = 0; i < slotViews.Length; i++)
            {
                var slot = MakeRect($"Slot{i + 1}", slotsRow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100, 140));
                Prefer(slot, 100, 140);
                var slotBg = AddImage(slot, Color.white, raycast: false);
                var slotIconRect = MakeRect("Icon", slot, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -4), new Vector2(-16, 78));
                var slotIcon = slotIconRect.gameObject.AddComponent<Image>();
                slotIcon.raycastTarget = false;
                var statusRect = MakeRect("Status", slot, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 22), new Vector2(-8, 34));
                var status = Label(statusRect, "Text", "Empty", 21, TextAnchor.MiddleCenter, Ink);
                var barBg = MakeRect("Bar", slot, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 6), new Vector2(-16, 14));
                AddImage(barBg, new Color(0.82f, 0.78f, 0.7f), raycast: false);
                var fillRect = Stretch("Fill", barBg, 2f);
                var fill = AddImage(fillRect, Green, raycast: false);
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                var view = slot.gameObject.AddComponent<QueueSlotView>();
                Wire(view, ("background", slotBg), ("icon", slotIcon), ("statusText", status), ("progressFill", fill));
                slotViews[i] = view;
            }

            var addSlot = MakeButton(slotsRow, "AddSlotButton", "+1 slot\n250", new Vector2(100, 140), Blue, out var addSlotLabel, 26);
            addSlotLabel.textWrappingMode = TMPro.TextWrappingModes.NoWrap; // shrink to fit instead of wrapping "+1 slot"
            addSlotLabel.margin = new Vector4(6f, 4f, 6f, 4f);

            var actions = MakeRect("Actions", queue, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0, 90));
            HRow(actions, 12, TextAnchor.MiddleLeft);
            var collect = MakeButton(actions, "CollectButton", "Collect", new Vector2(260, 86), Green, out var collectLabel, 32);
            var finish = MakeButton(actions, "FinishNowButton", "Finish now", new Vector2(300, 86), new Color(0.95f, 0.65f, 0.15f), out var finishLabel, 28);

            var panel = container.gameObject.AddComponent<FactoryPanel>();
            Wire(panel, ("panelRoot", root.gameObject), ("titleText", title), ("queueTitleText", queueTitle),
                ("recipesParent", recipes), ("recipeTemplate", recipeView), ("collectButton", collect), ("collectLabel", collectLabel),
                ("finishNowButton", finish), ("finishNowLabel", finishLabel),
                ("addSlotButton", addSlot), ("addSlotLabel", addSlotLabel));
            WireArray(panel, "slots", slotViews);
            root.gameObject.SetActive(false);
            return panel;
        }

        private static FieldPanel BuildFieldPanel(RectTransform safe)
        {
            var container = Stretch("FieldPanel", safe);
            var root = MakeRect("Root", container, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 196), new Vector2(1100, 260));
            AddImage(root, PanelColor);

            var titleRect = MakeRect("Title", root, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -10), new Vector2(-48, 54));
            var title = Label(titleRect, "Text", "Field", 38, TextAnchor.MiddleLeft, Ink, FontStyle.Bold);

            // Empty: crop buttons
            var crops = MakeRect("Crops", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            crops.offsetMin = new Vector2(24, 16);
            crops.offsetMax = new Vector2(-24, -66);
            HRow(crops, 20, TextAnchor.MiddleLeft);

            var template = MakeRect("CropTemplate", crops, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(250, 176));
            var bg = AddImage(template, Color.white);
            Prefer(template, 250, 176);
            var button = template.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            var iconRect = MakeRect("Icon", template, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -6), new Vector2(-20, 92));
            var icon = iconRect.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var nameRect = MakeRect("Name", template, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(-12, 40));
            var nameText = Label(nameRect, "Text", "Crop", 30, TextAnchor.MiddleCenter, Ink, FontStyle.Bold);
            var infoRect = MakeRect("Info", template, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 6), new Vector2(-12, 36));
            var infoText = Label(infoRect, "Text", "", 26, TextAnchor.MiddleCenter, Ink);
            var cropView = template.gameObject.AddComponent<CropButtonView>();
            Wire(cropView, ("button", button), ("icon", icon), ("nameText", nameText), ("infoText", infoText));
            template.gameObject.SetActive(false);

            // Growing / ready
            var growing = MakeRect("Growing", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            growing.offsetMin = new Vector2(24, 16);
            growing.offsetMax = new Vector2(-24, -66);
            var statusRect = MakeRect("Status", growing, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0, -10), new Vector2(680, 70));
            var status = Label(statusRect, "Text", "", 36, TextAnchor.MiddleLeft, Ink);
            var barBg = MakeRect("ProgressBar", growing, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0, 30), new Vector2(680, 36));
            AddImage(barBg, new Color(0.82f, 0.78f, 0.7f), raycast: false);
            var fillRect = Stretch("Fill", barBg, 4f);
            var fill = AddImage(fillRect, Green, raycast: false);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0.3f;
            var action = MakeButton(growing, "ActionButton", "Finish now", new Vector2(320, 140), new Color(0.95f, 0.65f, 0.15f), out var actionLabel, 34);
            var actionRect = (RectTransform)action.transform;
            actionRect.anchorMin = actionRect.anchorMax = actionRect.pivot = new Vector2(1f, 0.5f);
            actionRect.anchoredPosition = Vector2.zero;
            growing.gameObject.SetActive(false);

            var panel = container.gameObject.AddComponent<FieldPanel>();
            Wire(panel, ("panelRoot", root.gameObject), ("titleText", title), ("cropsView", crops.gameObject), ("cropsParent", crops),
                ("cropTemplate", cropView), ("growingView", growing.gameObject), ("statusText", status), ("progressFill", fill),
                ("actionButton", action), ("actionLabel", actionLabel));
            root.gameObject.SetActive(false);
            return panel;
        }

        /// <summary>Developer mode: "DEV" button top-right + a panel of testing shortcuts (hidden in Google Play builds).</summary>
        private static DevPanel BuildDevPanel(Transform canvas)
        {
            var container = Stretch("DevPanel", canvas);
            var open = MakeButton(container, "DevButton", "DEV", new Vector2(110, 76), new Color(0.55f, 0.2f, 0.6f), 30);
            var openRect = (RectTransform)open.transform;
            openRect.anchorMin = openRect.anchorMax = openRect.pivot = new Vector2(1f, 1f);
            openRect.anchoredPosition = new Vector2(-30, -20);

            var root = Stretch("Root", container);
            AddImage(root, Dim, rounded: false);
            var window = MakeRect("Window", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640, 1010));
            AddImage(window, PanelColor);
            VColumn(window, 16, TextAnchor.UpperCenter, 26);
            var title = Label(window, "Title", "Developer mode (test builds only)", 34, TextAnchor.MiddleCenter, Ink, FontStyle.Bold);
            Prefer(title, 560, 60);

            Button Row(string name, string label, Color color) => MakeButton(window, name, label, new Vector2(560, 96), color, 32);
            var coins = Row("CoinsButton", "+10,000 coins  +100 gems", new Color(0.95f, 0.65f, 0.15f));
            var levelUp = Row("LevelUpButton", "Level up (+1)", Blue);
            var unlockAll = Row("UnlockAllButton", "Unlock everything (level 10)", Blue);
            var fillBarn = Row("FillBarnButton", "Max barn + 10 of every item", new Color(0.62f, 0.38f, 0.25f));
            var finish = Row("FinishTimersButton", "Finish all timers + call train", Green);
            var land = Row("UnlockLandButton", "Unlock all land", Green);
            var dailyAgain = Row("DailyAgainButton", "Daily reward again", new Color(0.55f, 0.35f, 0.7f));
            var close = Row("CloseButton", "Close", Slate);

            var panel = container.gameObject.AddComponent<DevPanel>();
            Wire(panel, ("openButton", open), ("panelRoot", root.gameObject), ("closeButton", close), ("coinsButton", coins),
                ("levelUpButton", levelUp), ("unlockAllButton", unlockAll), ("fillBarnButton", fillBarn),
                ("finishTimersButton", finish), ("unlockLandButton", land), ("dailyAgainButton", dailyAgain));
            root.gameObject.SetActive(false);
            return panel;
        }

        private static LandPanel BuildLandPanel(RectTransform safe)
        {
            var container = Stretch("LandPanel", safe);
            var root = MakeRect("Root", container, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(1320, 300));
            AddImage(root, PanelColor);

            var title = Label(MakeRect("Title", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28, -12), new Vector2(700, 56)),
                "Text", "Land for sale", 42, TextAnchor.MiddleLeft, Ink, FontStyle.Bold);
            var info = Label(MakeRect("Info", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28, -68), new Vector2(700, 40)),
                "Text", "", 28, TextAnchor.MiddleLeft, new Color(0.4f, 0.38f, 0.34f));

            var costs = MakeRect("Costs", root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28, 40), new Vector2(880, 110));
            HRow(costs, 14, TextAnchor.MiddleLeft);
            var icons = new Image[4];
            var texts = new TMP_Text[4];
            for (int i = 0; i < 4; i++)
            {
                var chip = MakeRect($"Cost{i + 1}", costs, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(210, 100));
                Prefer(chip, 210, 100);
                AddImage(chip, Color.white, raycast: false);
                var iconRect = MakeRect("Icon", chip, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10, 0), new Vector2(64, 64));
                icons[i] = iconRect.gameObject.AddComponent<Image>();
                icons[i].preserveAspect = true;
                icons[i].raycastTarget = false;
                texts[i] = Label(MakeRect("Text", chip, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(38, 0), new Vector2(-88, -8)),
                    "Text", "", 26, TextAnchor.MiddleCenter, Ink, FontStyle.Bold);
            }

            var buy = MakeButton(root, "BuyButton", "Buy land", new Vector2(300, 120), Green, out var buyLabel, 40);
            var buyRect = (RectTransform)buy.transform;
            buyRect.anchorMin = buyRect.anchorMax = buyRect.pivot = new Vector2(1f, 0f);
            buyRect.anchoredPosition = new Vector2(-28, 34);
            var close = MakeButton(root, "CloseButton", "X", new Vector2(90, 90), Red, 44);
            var closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-20, -16);

            var panel = container.gameObject.AddComponent<LandPanel>();
            Wire(panel, ("panelRoot", root.gameObject), ("titleText", title), ("infoText", info), ("buyButton", buy), ("buyLabel", buyLabel),
                ("closeButton", close));
            WireArray(panel, "costIcons", icons);
            WireArray(panel, "costTexts", texts);
            root.gameObject.SetActive(false);
            return panel;
        }

        private static TrainPanel BuildTrainPanel(RectTransform safe)
        {
            var container = Stretch("TrainPanel", safe);
            var root = MakeRect("Root", container, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 196), new Vector2(1640, 340));
            AddImage(root, PanelColor);

            var titleRect = MakeRect("Title", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24, -10), new Vector2(600, 54));
            var title = Label(titleRect, "Text", "Train Station", 38, TextAnchor.MiddleLeft, Ink, FontStyle.Bold);
            var statusRect = MakeRect("Status", root, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -10), new Vector2(-700, 54));
            var status = Label(statusRect, "Text", "", 32, TextAnchor.MiddleCenter, Ink, FontStyle.Bold);

            var row = MakeRect("Crates", root, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            row.offsetMin = new Vector2(24, 16);
            row.offsetMax = new Vector2(24 + 6 * 200 + 5 * 10, -72);
            HRow(row, 10, TextAnchor.MiddleLeft);

            var crates = new TrainCrateView[6];
            for (int i = 0; i < crates.Length; i++)
            {
                var card = MakeRect($"Crate{i + 1}", row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200, 250));
                Prefer(card, 200, 250);
                var bg = AddImage(card, Color.white, raycast: false);
                var material = Label(MakeRect("Material", card, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -2), new Vector2(-8, 26)),
                    "Text", "", 19, TextAnchor.MiddleCenter, new Color(0.62f, 0.38f, 0.12f), FontStyle.Bold);
                var iconRect = MakeRect("Icon", card, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -26), new Vector2(-50, 70));
                var icon = iconRect.gameObject.AddComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                var name = Label(MakeRect("Name", card, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -98), new Vector2(-12, 32)),
                    "Text", "", 23, TextAnchor.MiddleCenter, Ink, FontStyle.Bold);
                var have = Label(MakeRect("Have", card, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -130), new Vector2(-12, 28)),
                    "Text", "", 22, TextAnchor.MiddleCenter, Ink);
                var reward = Label(MakeRect("Reward", card, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -158), new Vector2(-8, 28)),
                    "Text", "", 19, TextAnchor.MiddleCenter, new Color(0.45f, 0.42f, 0.38f));
                var load = MakeButton(card, "LoadButton", "Load", new Vector2(170, 52), Green, 28);
                var loadRect = (RectTransform)load.transform;
                loadRect.anchorMin = loadRect.anchorMax = loadRect.pivot = new Vector2(0.5f, 0f);
                loadRect.anchoredPosition = new Vector2(0, 10);
                var view = card.gameObject.AddComponent<TrainCrateView>();
                Wire(view, ("background", bg), ("icon", icon), ("nameText", name), ("haveText", have), ("rewardText", reward), ("loadButton", load),
                    ("materialText", material));
                crates[i] = view;
            }

            var send = MakeButton(root, "SendButton", "Send train", new Vector2(300, 130), Blue, out var sendLabel, 32);
            var sendRect = (RectTransform)send.transform;
            sendRect.anchorMin = sendRect.anchorMax = sendRect.pivot = new Vector2(1f, 0.5f);
            sendRect.anchoredPosition = new Vector2(-30, -30);
            var call = MakeButton(root, "CallButton", "Call now", new Vector2(320, 130), new Color(0.95f, 0.65f, 0.15f), out var callLabel, 30);
            var callRect = (RectTransform)call.transform;
            callRect.anchorMin = callRect.anchorMax = callRect.pivot = new Vector2(0.5f, 0.5f);
            callRect.anchoredPosition = new Vector2(0, -40);

            var panel = container.gameObject.AddComponent<TrainPanel>();
            Wire(panel, ("panelRoot", root.gameObject), ("titleText", title), ("statusText", status), ("cratesRow", row.gameObject),
                ("sendButton", send), ("sendLabel", sendLabel), ("callButton", call), ("callLabel", callLabel));
            WireArray(panel, "crates", crates);
            root.gameObject.SetActive(false);
            return panel;
        }

        private static TasksPanel BuildTasksPanel(RectTransform safe)
        {
            const float width = 470f, rowHeight = 96f;
            var container = MakeRect("TasksPanel", safe, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30, -128),
                new Vector2(width, 52 + 3 * (rowHeight + 8) + 12));
            var root = Stretch("Root", container);
            AddImage(root, new Color(PanelColor.r, PanelColor.g, PanelColor.b, 0.94f), raycast: false);

            var titleRect = MakeRect("Title", root, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -6), new Vector2(-28, 44));
            Label(titleRect, "Text", "Tasks", 30, TextAnchor.MiddleLeft, Ink, FontStyle.Bold);

            var rows = new TaskRowView[3];
            for (int i = 0; i < rows.Length; i++)
            {
                var row = MakeRect($"Task{i + 1}", root, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0, -52 - i * (rowHeight + 8)), new Vector2(-24, rowHeight));
                AddImage(row, Color.white, raycast: false);

                var title = Label(MakeRect("Title", row, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(14, -6), new Vector2(-150, 40)),
                    "Text", "Task", 27, TextAnchor.MiddleLeft, Ink, FontStyle.Bold);
                var progress = Label(MakeRect("Progress", row, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14, -6), new Vector2(120, 40)),
                    "Text", "0/1", 26, TextAnchor.MiddleRight, Ink, FontStyle.Bold);

                var barBg = MakeRect("Bar", row, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(14, 38), new Vector2(290, 12));
                AddImage(barBg, new Color(0.82f, 0.78f, 0.7f), raycast: false);
                var fill = AddImage(Stretch("Fill", barBg, 2f), Green, raycast: false);
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;

                var reward = Label(MakeRect("Reward", row, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(14, 6), new Vector2(300, 30)),
                    "Text", "", 21, TextAnchor.MiddleLeft, new Color(0.45f, 0.42f, 0.38f));

                var claim = MakeButton(row, "ClaimButton", "Claim", new Vector2(124, 66), Green, 30);
                var claimRect = (RectTransform)claim.transform;
                claimRect.anchorMin = claimRect.anchorMax = claimRect.pivot = new Vector2(1f, 0.5f);
                claimRect.anchoredPosition = new Vector2(-12, -6);

                var view = row.gameObject.AddComponent<TaskRowView>();
                Wire(view, ("titleText", title), ("progressText", progress), ("rewardText", reward), ("progressFill", fill), ("claimButton", claim));
                rows[i] = view;
            }

            var panel = container.gameObject.AddComponent<TasksPanel>();
            Wire(panel, ("panelRoot", root.gameObject));
            WireArray(panel, "rows", rows);
            return panel;
        }

        /// <summary>
        /// Turns <paramref name="viewport"/> into a sideways-scrolling list (clipped, drag/fling with elastic edges) and
        /// returns the content row that cards are added to. The content grows with the number of cards.
        /// </summary>
        private static RectTransform HorizontalScrollList(RectTransform viewport, string contentName, float spacing)
        {
            viewport.gameObject.AddComponent<RectMask2D>();
            AddImage(viewport, new Color(1f, 1f, 1f, 0f), rounded: false); // invisible, but catches drags between cards

            var content = MakeRect(contentName, viewport, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            HRow(content, spacing, TextAnchor.MiddleLeft);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 30f;
            return content;
        }

        /// <summary>Full-screen layer behind the rest of the HUD for "+2 Wheat" labels (first child = drawn first).</summary>
        private static FloatingTextLayer BuildFloatingTextLayer(Canvas canvas, Camera cam)
        {
            var rect = Stretch("FloatingTextLayer", canvas.transform);
            var layer = rect.gameObject.AddComponent<FloatingTextLayer>();
            Wire(layer, ("worldCamera", cam), ("font", UIFontAsset),
                ("fontMaterial", AssetDatabase.LoadAssetAtPath<Material>(ProjectSetup.UIFontOutlineMaterialPath)));
            return layer;
        }

        private static ToastView BuildToast(RectTransform safe)
        {
            var rect = MakeRect("Toast", safe, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 560), new Vector2(1000, 84));
            AddImage(rect, new Color(0.1f, 0.12f, 0.16f, 0.85f), raycast: false);
            var text = Label(rect, "Text", "", 38, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            var group = rect.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f; // invisible until the first message
            group.blocksRaycasts = false;
            var toast = rect.gameObject.AddComponent<ToastView>();
            Wire(toast, ("messageText", text));
            return toast;
        }

        private static TMP_Text Pill(RectTransform parent, string name, float width)
        {
            var rt = MakeRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, 76));
            AddImage(rt, PillColor);
            Prefer(rt, width, 76);
            return Label(rt, "Text", "-", 38, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
        }

        private static BuildingShopPanel BuildShop(Transform canvas)
        {
            var container = Stretch("BuildingShopPanel", canvas);
            var root = Stretch("Root", container);
            AddImage(root, Dim, rounded: false);

            var window = MakeRect("Window", root, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(-120, 470));
            AddImage(window, PanelColor);

            var titleRect = MakeRect("Title", window, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -16), new Vector2(-80, 70));
            Label(titleRect, "Text", "Build", 52, TextAnchor.MiddleLeft, Ink, FontStyle.Bold);

            // Tabs next to the title.
            var tabs = MakeRect("Tabs", window, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(220, -16), new Vector2(600, 74));
            HRow(tabs, 14, TextAnchor.MiddleLeft);
            var buildingsTab = MakeButton(tabs, "BuildingsTab", "Buildings", new Vector2(260, 70), Green, 32);
            var decorationsTab = MakeButton(tabs, "DecorationsTab", "Decorations", new Vector2(280, 70), Slate, 32);

            var close = MakeButton(window, "CloseButton", "X", new Vector2(90, 90), Red, 44);
            var closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-20, -16);

            var viewport = MakeRect("Viewport", window, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            viewport.offsetMin = new Vector2(30, 24);
            viewport.offsetMax = new Vector2(-30, -100);
            var items = HorizontalScrollList(viewport, "Items", 24);

            // Item template (cloned per building by BuildingShopPanel)
            var template = MakeRect("ItemTemplate", items, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260, 330));
            var bg = AddImage(template, Color.white);
            Prefer(template, 260, 330);
            var button = template.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;

            var iconRect = MakeRect("Icon", template, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -12), new Vector2(-24, 200));
            var icon = iconRect.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var nameRect = MakeRect("Name", template, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 62), new Vector2(-16, 50));
            var nameText = Label(nameRect, "Text", "Name", 32, TextAnchor.MiddleCenter, Ink, FontStyle.Bold);
            var priceRect = MakeRect("Price", template, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 14), new Vector2(-16, 46));
            var priceText = Label(priceRect, "Text", "0", 30, TextAnchor.MiddleCenter, Ink);

            var itemView = template.gameObject.AddComponent<ShopItemView>();
            Wire(itemView, ("button", button), ("icon", icon), ("nameText", nameText), ("priceText", priceText));
            template.gameObject.SetActive(false);

            var panel = container.gameObject.AddComponent<BuildingShopPanel>();
            Wire(panel, ("panelRoot", root.gameObject), ("itemsParent", items), ("itemTemplate", itemView), ("closeButton", close),
                ("buildingsTab", buildingsTab), ("decorationsTab", decorationsTab),
                ("buildingsTabImage", buildingsTab.GetComponent<Image>()), ("decorationsTabImage", decorationsTab.GetComponent<Image>()),
                ("scroll", viewport.GetComponent<ScrollRect>()));
            root.gameObject.SetActive(false);
            return panel;
        }

        private static ConfirmDialog CreateConfirmDialog(Transform canvas)
        {
            var container = Stretch("ConfirmDialog", canvas);
            var root = Stretch("Root", container);
            AddImage(root, Dim, rounded: false);

            var window = MakeRect("Window", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860, 420));
            AddImage(window, PanelColor);
            var messageRect = MakeRect("Message", window, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -30), new Vector2(-80, 220));
            var message = Label(messageRect, "Text", "Are you sure?", 40, TextAnchor.MiddleCenter, Ink);

            var buttons = MakeRect("Buttons", window, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(-80, 120));
            HRow(buttons, 40, TextAnchor.MiddleCenter);
            var cancel = MakeButton(buttons, "CancelButton", "Cancel", new Vector2(280, 110), Slate);
            var confirm = MakeButton(buttons, "ConfirmButton", "OK", new Vector2(280, 110), Red, out var confirmLabel);

            var dialog = container.gameObject.AddComponent<ConfirmDialog>();
            Wire(dialog, ("dialogRoot", root.gameObject), ("messageText", message), ("confirmButton", confirm),
                ("confirmLabel", confirmLabel), ("cancelButton", cancel));
            root.gameObject.SetActive(false);
            return dialog;
        }

        // ================================================================== Shared

        private static Camera CreateCamera(string name, Transform parent, Color background, float orthographicSize)
        {
            var go = Go(name, parent);
            go.tag = "MainCamera";
            go.transform.localPosition = new Vector3(0f, 0f, -10f);
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = orthographicSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;
            go.AddComponent<AudioListener>();
            return cam;
        }

        private static Canvas CreateCanvas(Transform parent)
        {
            var go = new GameObject("Canvas", typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            go.AddComponent<ButtonSounds>();
            return canvas;
        }

        private static void CreateEventSystem(Transform parent)
        {
            var go = Go("EventSystem", parent);
            go.AddComponent<EventSystem>();
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }
    }
}
