using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            PaintStarterMap(ground, road, water, art);

            var isoGrid = gridGo.AddComponent<IsoGrid>();
            Wire(isoGrid, ("grid", grid), ("groundTilemap", ground));
            WireArray(isoGrid, "blockingTilemaps", water, road);

            var buildingsRoot = Go("Buildings", world.transform);
            Go("Crops", world.transform);
            Go("Decorations", world.transform);
            Go("Characters", world.transform);
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
            Wire(production, ("buildings", buildingController), ("placement", placement));
            Wire(farming, ("buildings", buildingController), ("placement", placement));

            // --- UI
            var uiRoot = Go("UI");
            var canvas = CreateCanvas(uiRoot.transform);
            CreateEventSystem(uiRoot.transform);
            var hud = BuildTownHud(canvas);

            // --- Audio
            var audioGo = Go("Audio");
            var audioManagerGo = Go("AudioManager", audioGo.transform);
            var music = audioManagerGo.AddComponent<AudioSource>();
            music.loop = true;
            music.playOnAwake = true;
            var sfx = Go("Sfx", audioManagerGo.transform).AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            var audioManager = audioManagerGo.AddComponent<AudioManager>();
            Wire(audioManager, ("musicSource", music), ("sfxSource", sfx));

            Wire(sceneController,
                ("fallbackContent", content),
                ("grid", isoGrid),
                ("cameraController", cameraController),
                ("buildings", buildingController),
                ("placement", placement),
                ("farming", farming),
                ("production", production),
                ("hud", hud));

            EditorSceneManager.SaveScene(scene, TownPath);
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
            var nav = MakeRect("BottomNavigation", safe, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30, 30), new Vector2(1100, 130));
            HRow(nav, 24, TextAnchor.LowerRight);
            var adButton = MakeButton(nav, "RewardedAdButton", "Watch ad\n+100 coins", new Vector2(300, 120), new Color(0.95f, 0.65f, 0.15f), out var adLabel, 34);
            var rewarded = adButton.gameObject.AddComponent<RewardedCoinsButton>();
            Wire(rewarded, ("button", adButton), ("label", adLabel));
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

            // --- Farming: field panel (above the selection bar) and toast
            var fieldPanel = BuildFieldPanel(safe);
            var factoryPanel = BuildFactoryPanel(safe);
            var toast = BuildToast(safe);

            // --- Barn screen
            var barnPanel = BuildBarnPanel(canvas.transform);

            // --- Shop
            var shop = BuildShop(canvas.transform);

            // --- Confirm dialog (last = drawn on top)
            var dialog = CreateConfirmDialog(canvas.transform);

            var hud = canvas.gameObject.AddComponent<TownHudController>();
            Wire(hud,
                ("fieldPanel", fieldPanel),
                ("factoryPanel", factoryPanel),
                ("barnPanel", barnPanel),
                ("barnButton", barnButton),
                ("barnPillButton", barnPillButton),
                ("toast", toast),
                ("topBar", topBar),
                ("shopPanel", shop),
                ("confirmDialog", dialog),
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

        private static FactoryPanel BuildFactoryPanel(RectTransform safe)
        {
            var container = Stretch("FactoryPanel", safe);
            var root = MakeRect("Root", container, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 196), new Vector2(1640, 330));
            AddImage(root, PanelColor);

            var titleRect = MakeRect("Title", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24, -10), new Vector2(600, 54));
            var title = Label(titleRect, "Text", "Bakery", 38, TextAnchor.MiddleLeft, Ink, FontStyle.Bold);
            var queueTitleRect = MakeRect("QueueTitle", root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24, -10), new Vector2(620, 54));
            var queueTitle = Label(queueTitleRect, "Text", "Queue 0/3", 32, TextAnchor.MiddleLeft, Ink, FontStyle.Bold);

            // Recipes (left)
            var recipes = MakeRect("Recipes", root, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(960, 0));
            recipes.offsetMin = new Vector2(24, 16);
            recipes.offsetMax = new Vector2(984, -70);
            HRow(recipes, 16, TextAnchor.MiddleLeft);

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
            queue.offsetMin = new Vector2(-640, 16);
            queue.offsetMax = new Vector2(-24, -70);
            var slotsRow = MakeRect("Slots", queue, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0, 140));
            HRow(slotsRow, 12, TextAnchor.UpperLeft);

            var slotViews = new QueueSlotView[4];
            for (int i = 0; i < slotViews.Length; i++)
            {
                var slot = MakeRect($"Slot{i + 1}", slotsRow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(144, 140));
                Prefer(slot, 144, 140);
                var slotBg = AddImage(slot, Color.white, raycast: false);
                var slotIconRect = MakeRect("Icon", slot, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -4), new Vector2(-16, 78));
                var slotIcon = slotIconRect.gameObject.AddComponent<Image>();
                slotIcon.raycastTarget = false;
                var statusRect = MakeRect("Status", slot, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 22), new Vector2(-8, 34));
                var status = Label(statusRect, "Text", "Empty", 24, TextAnchor.MiddleCenter, Ink);
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

            var actions = MakeRect("Actions", queue, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0, 90));
            HRow(actions, 12, TextAnchor.MiddleLeft);
            var collect = MakeButton(actions, "CollectButton", "Collect", new Vector2(260, 86), Green, out var collectLabel, 32);
            var finish = MakeButton(actions, "FinishNowButton", "Finish now", new Vector2(300, 86), new Color(0.95f, 0.65f, 0.15f), out var finishLabel, 28);

            var panel = container.gameObject.AddComponent<FactoryPanel>();
            Wire(panel, ("panelRoot", root.gameObject), ("titleText", title), ("queueTitleText", queueTitle),
                ("recipesParent", recipes), ("recipeTemplate", recipeView), ("collectButton", collect), ("collectLabel", collectLabel),
                ("finishNowButton", finish), ("finishNowLabel", finishLabel));
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

        private static Text Pill(RectTransform parent, string name, float width)
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

            var close = MakeButton(window, "CloseButton", "X", new Vector2(90, 90), Red, 44);
            var closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-20, -16);

            var items = MakeRect("Items", window, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            items.offsetMin = new Vector2(30, 24);
            items.offsetMax = new Vector2(-30, -100);
            HRow(items, 24, TextAnchor.MiddleLeft);

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
            Wire(panel, ("panelRoot", root.gameObject), ("itemsParent", items), ("itemTemplate", itemView), ("closeButton", close));
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
