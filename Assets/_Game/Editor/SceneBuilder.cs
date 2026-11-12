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

            var settingsPanel = BuildSettingsPanel(canvas.transform);

            var dialog = CreateConfirmDialog(canvas.transform);
            BuildQuitDialog(canvas.transform); // last: drawn on top, handles the phone's back button

            var controllerGo = Go("MainMenuController");
            var controller = controllerGo.AddComponent<MainMenuController>();
            Wire(controller,
                ("fallbackContent", content),
                ("continueButton", continueButton),
                ("newGameButton", newGameButton),
                ("settingsButton", settingsButton),
                ("statusText", status),
                ("confirmDialog", dialog),
                ("settingsPanel", settingsPanel));

            EditorSceneManager.SaveScene(scene, MainMenuPath);
        }

        public const string CreditsTextPath = "Assets/_Game/Data/Text/Credits.txt";

        /// <summary>
        /// Quit dialog + phone back button (v0.39.0): wooden frame with the LEAVING? sign, a friendly message, green Stay
        /// and red Quit. <see cref="BackButtonHandler"/> opens it when the back button is pressed with nothing else open.
        /// </summary>
        private static BackButtonHandler BuildQuitDialog(Transform canvas)
        {
            var outline = AssetDatabase.LoadAssetAtPath<Material>(ProjectSetup.UIFontOutlineMaterialPath);
            var container = Stretch("QuitDialog", canvas);
            var root = Stretch("Root", container);
            AddImage(root, Dim, rounded: false);
            var frame = BuildShopFrame(root, "Window", "LEAVING?", out _, out var close, out _, new Vector2(1120, 640));
            var message = Label(MakeRect("Message", frame, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(-160, -150)),
                "Text", "Leaving already?\nYour town is saved and will be waiting for you.", 44, TextAnchor.MiddleCenter, new Color(0.40f, 0.24f, 0.10f));
            var buttons = MakeRect("Buttons", frame, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 70), new Vector2(-160, 130));
            HRow(buttons, 60, TextAnchor.MiddleCenter);
            var stay = MakeButton(buttons, "StayButton", "Stay", new Vector2(360, 130), Green, out var stayLabel, 56);
            var quit = MakeButton(buttons, "QuitButton", "Quit", new Vector2(360, 130), Red, out var quitLabel, 56);
            if (outline != null)
            {
                stayLabel.fontSharedMaterial = outline;
                quitLabel.fontSharedMaterial = outline;
            }

            var handler = container.gameObject.AddComponent<BackButtonHandler>();
            Wire(handler, ("quitRoot", root.gameObject), ("stayButton", stay), ("quitButton", quit), ("closeButton", close), ("messageText", message));
            root.gameObject.SetActive(false);
            return handler;
        }

        /// <summary>
        /// Settings window (v0.35.0), shared by the main menu and the town: wooden frame with the SETTINGS sign, music and
        /// sound sliders, Privacy policy / Credits / Privacy and ads buttons, Main menu (town only), version. Credits is a
        /// second framed window with the scrolling credits and font licence.
        /// </summary>
        private static SettingsPanel BuildSettingsPanel(Transform canvas)
        {
            var container = Stretch("SettingsPanel", canvas);
            var root = Stretch("Root", container);
            AddImage(root, Dim, rounded: false);
            var frame = BuildShopFrame(root, "Window", "SETTINGS", out _, out var close, out _, new Vector2(1240, 880));

            var body = Stretch("Body", frame);
            body.offsetMin = new Vector2(80, 60);
            body.offsetMax = new Vector2(-80, -130);
            VColumn(body, 22, TextAnchor.UpperCenter);
            var music = SliderRow(body, "Music");
            var sfx = SliderRow(body, "Sound effects");

            var links = MakeRect("Links", body, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980, 100));
            Prefer(links, 980, 100);
            HRow(links, 24, TextAnchor.MiddleCenter);
            var policy = MakeButton(links, "PrivacyPolicyButton", "Privacy policy", new Vector2(310, 100), Blue, 36);
            var credits = MakeButton(links, "CreditsButton", "Credits", new Vector2(250, 100), Blue, 36);
            var consent = MakeButton(links, "PrivacyOptionsButton", "Privacy & ads", new Vector2(310, 100), Slate, 36);

            var mainMenu = MakeButton(body, "MainMenuButton", "Main menu", new Vector2(420, 110), Green, 44);
            var message = Label(body, "Message", "", 30, TextAnchor.MiddleCenter, new Color(0.6f, 0.25f, 0.1f));
            Prefer(message, 980, 70);
            var version = Label(body, "Version", "Version", 26, TextAnchor.MiddleCenter, new Color(0.45f, 0.42f, 0.38f));
            Prefer(version, 980, 40);

            // Credits: a second framed window over the settings, with the credits text scrolling up and down.
            var creditsRoot = Stretch("CreditsRoot", root);
            AddImage(creditsRoot, Dim, rounded: false);
            var creditsFrame = BuildShopFrame(creditsRoot, "CreditsWindow", "CREDITS", out _, out var creditsClose, out _, new Vector2(1600, 900));
            var viewport = Stretch("Viewport", creditsFrame);
            viewport.offsetMin = new Vector2(70, 60);
            viewport.offsetMax = new Vector2(-70, -120);
            viewport.gameObject.AddComponent<RectMask2D>();
            AddImage(viewport, new Color(1f, 1f, 1f, 0f), rounded: false); // invisible, catches drags
            var content = MakeRect("Content", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0, 100));
            var creditsText = AddText(content.gameObject, "Credits", 28, TextAnchor.UpperLeft, Ink);
            creditsText.margin = new Vector4(10f, 10f, 10f, 30f);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 30f;

            var panel = container.gameObject.AddComponent<SettingsPanel>();
            Wire(panel, ("panelRoot", root.gameObject), ("musicSlider", music), ("sfxSlider", sfx), ("closeButton", close),
                ("privacyOptionsButton", consent), ("privacyPolicyButton", policy), ("creditsButton", credits), ("mainMenuButton", mainMenu),
                ("messageText", message), ("versionText", version), ("creditsRoot", creditsRoot.gameObject), ("creditsText", creditsText),
                ("creditsCloseButton", creditsClose), ("creditsScroll", scroll),
                ("creditsSource", AssetDatabase.LoadAssetAtPath<TextAsset>(CreditsTextPath)));
            creditsRoot.gameObject.SetActive(false);
            root.gameObject.SetActive(false);
            return panel;
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
            var truck = Go("TruckController", systems.transform).AddComponent<TruckController>();
            var homes = Go("HappinessController", systems.transform).AddComponent<HappinessController>();
            var seasons = Go("SeasonController", world.transform).AddComponent<SeasonController>();
            var landscape = Go("LandscapeController", systems.transform).AddComponent<LandscapeController>();
            var seasonArt = PlaceholderArtGenerator.LoadSeasonArt();
            Wire(seasons, ("groundTilemap", ground), ("buildings", buildingController), ("placement", placement));
            WireArray(seasons, "grassTiles", seasonArt.Grass);
            WireArray(seasons, "fallingSprites", seasonArt.Falling);
            WireArray(seasons, "treeSprites", seasonArt.Trees);
            var streetLife = Go("StreetLife", world.transform).AddComponent<StreetLife>();
            Wire(landscape, ("grid", isoGrid), ("pointerInput", input), ("placement", placement), ("seasons", seasons), ("streetLife", streetLife),
                ("groundTilemap", ground), ("roadTilemap", road), ("waterTilemap", water), ("railTilemap", rail),
                ("cellSprite", art.CellHighlight), ("flagSprite", AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.RouteFlagPath)));
            WireArray(landscape, "tiles", PlaceholderArtGenerator.LoadLandscapeTiles());
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
            Wire(truck, ("buildings", buildingController),
                ("truckIcon", AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.TruckIconPath)));
            Wire(homes, ("buildings", buildingController), ("placement", placement),
                ("coinIcon", AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.CoinIconPath)));
            Wire(construction, ("buildings", buildingController), ("placement", placement),
                ("constructionIcon", AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.ConstructionIconPath)));
            Wire(production, ("buildings", buildingController), ("placement", placement));
            Wire(farming, ("buildings", buildingController), ("placement", placement),
                ("cropMaterial", AssetDatabase.LoadAssetAtPath<Material>(ProjectSetup.SwayMaterialPath)));
            var smoke = Go("ChimneySmoke", world.transform).AddComponent<ChimneySmoke>();
            Wire(smoke, ("buildings", buildingController), ("production", production),
                ("puffSprite", AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.SmokePuffPath)));
            var life = Go("FactoryLife", world.transform).AddComponent<FactoryLife>();
            Wire(life, ("buildings", buildingController), ("production", production),
                ("sleepSprite", AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.SleepZPath)),
                ("grainSprite", AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.GrainPath)),
                ("dustSprite", AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.SmokePuffPath)),
                ("sackSprite", AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.CarrySackPath)));
            WireArray(life, "workerSprites", Enumerable.Range(1, PlaceholderArtGenerator.PersonVariants)
                .Select(i => AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.PersonSpritePath(i))).ToArray());
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
                ("truckController", truck),
                ("happinessController", homes),
                ("seasonController", seasons),
                ("landscapeController", landscape),
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
            // Joined road tiles (v0.36.0): kerbs only where a road ends or turns.
            TileBase roadTile = AssetDatabase.LoadAssetAtPath<JoinedRoadTile>(RoadTileBuilder.TilePath(TownBuilder.Domain.Terrain.Road));
            if (roadTile == null) roadTile = art.Road;
            road.SetTiles(roadCells.ToArray(), Enumerable.Repeat(roadTile, roadCells.Count).ToArray());
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
            // Left: level star + XP bar. Right: coins, gems, barn, then the round menu button.
            var topBarRect = MakeRect("TopBar", safe, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -16), new Vector2(-48, 120));
            var levelText = LevelBadge(topBarRect, out var xpFill, out var xpText, out var levelPill);

            var right = MakeRect("RightGroup", topBarRect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(1240, 120));
            HRow(right, 40, TextAnchor.MiddleRight);
            var coinsText = Pill(right, "CoinsPill", 300, PlaceholderArtGenerator.CoinIconPath);
            var gemsText = Pill(right, "GemsPill", 230, PlaceholderArtGenerator.GemIconPath);
            var barnText = Pill(right, "BarnPill", 270, PlaceholderArtGenerator.UiArtFolder + "/PH_Icon_Barn.png");
            var menuButton = MakeButton(right, "MenuButton", "", new Vector2(104, 104), new Color(0.18f, 0.52f, 0.95f), 30);
            var menuImg = menuButton.GetComponent<Image>();
            menuImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.CirclePath);
            menuImg.type = Image.Type.Simple;
            var menuIcon = MakeRect("Icon", (RectTransform)menuButton.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54, 45));
            var menuIconImg = menuIcon.gameObject.AddComponent<Image>();
            menuIconImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.MenuIconPath);
            menuIconImg.raycastTarget = false;

            var barnPill = barnText.transform.parent.gameObject;
            var barnPillButton = barnPill.AddComponent<Button>();
            barnPillButton.targetGraphic = barnPill.GetComponent<Image>();
            var topBar = topBarRect.gameObject.AddComponent<TopBarView>();
            Wire(topBar, ("levelText", levelText), ("coinsText", coinsText), ("gemsText", gemsText), ("barnText", barnText),
                ("xpFill", xpFill), ("xpText", xpText));

            // --- Bottom navigation
            var nav = MakeRect("BottomNavigation", safe, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30, 30), new Vector2(1336, 130));
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
            var buildButton = MakeButton(nav, "BuildButton", "Build", new Vector2(260, 120), Green, 48);

            // --- Placement bar
            var placementBar = MakeRect("PlacementActionBar", safe, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(1260, 160));
            AddImage(placementBar, PanelColor);
            HRow(placementBar, 24, TextAnchor.MiddleCenter, 24);
            var placementInfo = Label(placementBar, "Info", "", 34, TextAnchor.MiddleLeft, Ink);
            Prefer(placementInfo, 600, 120);
            var rotatePlacement = MakeButton(placementBar, "RotateButton", "Rotate", new Vector2(180, 110), new Color(0.13f, 0.58f, 0.6f));
            var cancelPlacement = MakeButton(placementBar, "CancelButton", "Cancel", new Vector2(180, 110), Red);
            var confirmPlacement = MakeButton(placementBar, "ConfirmButton", "Place", new Vector2(180, 110), Green);

            // --- Selection bar
            var selectionBar = MakeRect("SelectionActionBar", safe, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(1200, 150));
            AddImage(selectionBar, PanelColor);
            HRow(selectionBar, 24, TextAnchor.MiddleCenter, 22);
            var selectionName = Label(selectionBar, "Name", "", 42, TextAnchor.MiddleLeft, Ink, FontStyle.Bold);
            Prefer(selectionName, 320, 100);
            var moveButton = MakeButton(selectionBar, "MoveButton", "Move", new Vector2(180, 100), Blue);
            var rotateButton = MakeButton(selectionBar, "RotateButton", "Rotate", new Vector2(180, 100), new Color(0.13f, 0.58f, 0.6f));
            var removeButton = MakeButton(selectionBar, "RemoveButton", "Remove", new Vector2(190, 100), Red);
            var deselectButton = MakeButton(selectionBar, "CloseButton", "Close", new Vector2(170, 100), Slate);

            // --- Town tasks (top-left, under the top bar)
            var tasksPanel = BuildTasksPanel(safe);

            // --- Farming: field panel (above the selection bar) and toast
            var fieldPanel = BuildFieldPanel(safe);
            var factoryPanel = BuildFactoryPanel(safe);
            var trainPanel = BuildTrainPanel(safe);
            var truckPanel = BuildTruckPanel(safe);
            var landPanel = BuildLandPanel(safe);
            var constructionPanel = BuildConstructionPanel(safe);
            var homePanel = BuildHomePanel(safe);
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
            var eventPanel = BuildEventPanel(canvas.transform, safe);
            var collectionsPanel = BuildCollectionsPanel(canvas.transform, safe);
            var landscapePanel = BuildLandscapePanel(safe, nav.gameObject, tasksPanel.gameObject);
            tasksPanel.transform.SetAsLastSibling(); // the open list covers the Event button under it
            var daily = BuildDailyRewardDialog(canvas.transform);
            var settingsPanel = BuildSettingsPanel(canvas.transform);
            var rewardLayer = Stretch("RewardFlyLayer", canvas.transform);
            var rewardFlyer = rewardLayer.gameObject.AddComponent<RewardFlyer>();
            Wire(rewardFlyer, ("coinsTarget", (RectTransform)coinsText.transform.parent), ("gemsTarget", (RectTransform)gemsText.transform.parent),
                ("xpTarget", levelPill),
                ("coinSprite", AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.CoinIconPath)),
                ("gemSprite", AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.GemIconPath)),
                ("xpSprite", AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.XpIconPath)));
            var devPanel = BuildDevPanel(canvas.transform);
            var backHandler = BuildQuitDialog(canvas.transform); // last: drawn on top, handles the phone's back button

            // Every panel and action bar fades/slides in when shown.
            foreach (var t in canvas.GetComponentsInChildren<Transform>(true))
                if (t.name == "Root" || t.name == "SelectionActionBar" || t.name == "PlacementActionBar")
                    t.gameObject.AddComponent<PanelTransition>();

            var hud = canvas.gameObject.AddComponent<TownHudController>();
            Wire(hud,
                ("fieldPanel", fieldPanel),
                ("factoryPanel", factoryPanel),
                ("trainPanel", trainPanel),
                ("truckPanel", truckPanel),
                ("landPanel", landPanel),
                ("constructionPanel", constructionPanel),
                ("homePanel", homePanel),
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
                ("eventPanel", eventPanel),
                ("collectionsPanel", collectionsPanel),
                ("landscapePanel", landscapePanel),
                ("rewardFlyer", rewardFlyer),
                ("bottomNavigation", nav.gameObject),
                ("buildButton", buildButton),
                ("rewardedCoinsButton", rewarded),
                ("menuButton", menuButton),
                ("settingsPanel", settingsPanel),
                ("backButton", backHandler),
                ("placementBar", placementBar.gameObject),
                ("placementInfoText", placementInfo),
                ("confirmPlacementButton", confirmPlacement),
                ("cancelPlacementButton", cancelPlacement),
                ("rotatePlacementButton", rotatePlacement),
                ("rotateButton", rotateButton),
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

        /// <summary>
        /// Weekend event: an orange button on the left of the town (under the tasks) and the event window with three
        /// prize rows. Both are hidden on weekdays (EventPanel shows them during an event).
        /// </summary>
        private static EventPanel BuildEventPanel(Transform canvas, RectTransform safe)
        {
            var outline = AssetDatabase.LoadAssetAtPath<Material>(ProjectSetup.UIFontOutlineMaterialPath);
            var brown = new Color(0.40f, 0.24f, 0.10f);
            var orange = new Color(0.97f, 0.55f, 0.12f);

            // --- Button in the town
            var buttonRect = MakeRect("EventButton", safe, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30, -546), new Vector2(330, 118));
            var buttonBg = AddImage(buttonRect, orange);
            var open = buttonRect.gameObject.AddComponent<Button>();
            open.targetGraphic = buttonBg;
            var star = MakeRect("Icon", buttonRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(50, 0), new Vector2(92, 92));
            var starImg = star.gameObject.AddComponent<Image>();
            starImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.GemIconPath);
            starImg.preserveAspect = true;
            starImg.raycastTarget = false;
            var bTitle = Label(MakeRect("Title", buttonRect, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(46, -4), new Vector2(-110, 0)),
                "Text", "Event", 30, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            bTitle.enableAutoSizing = true;
            bTitle.fontSizeMin = 20;
            bTitle.fontSizeMax = 32;
            bTitle.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            if (outline != null) bTitle.fontSharedMaterial = outline;
            var bTimer = Label(MakeRect("Timer", buttonRect, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(46, 6), new Vector2(-110, 0)),
                "Text", "1d 4h", 28, TextAnchor.MiddleLeft, new Color(1f, 0.97f, 0.85f), FontStyle.Bold);
            var badge = MakeRect("Badge", buttonRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-6, -6), new Vector2(54, 54));
            var badgeImg = badge.gameObject.AddComponent<Image>();
            badgeImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.CirclePath);
            badgeImg.color = new Color(0.90f, 0.17f, 0.14f);
            badgeImg.raycastTarget = false;
            var badgeText = Label(badge, "Text", "!", 34, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            if (outline != null) badgeText.fontSharedMaterial = outline;
            badge.gameObject.SetActive(false);
            buttonRect.gameObject.SetActive(false);

            // --- Window
            var container = Stretch("EventPanel", canvas);
            var root = Stretch("Root", container);
            AddImage(root, Dim, rounded: false);
            var frame = MakeRect("Window", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(1360, 880));
            AddImage(frame, orange);
            var inner = Stretch("Inner", frame, 14f);
            AddImage(inner, new Color(1f, 0.96f, 0.86f));

            var title = Label(MakeRect("Title", inner, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -16), new Vector2(-260, 90)),
                "Text", "Harvest Festival", 64, TextAnchor.MiddleCenter, new Color(0.88f, 0.42f, 0.08f), FontStyle.Bold);
            var desc = Label(MakeRect("Description", inner, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -104), new Vector2(-160, 56)),
                "Text", "", 32, TextAnchor.MiddleCenter, brown);
            var timer = Label(MakeRect("Timer", inner, new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(60, -170), new Vector2(-60, 56)),
                "Text", "Ends in 1d 4h", 36, TextAnchor.MiddleLeft, new Color(0.80f, 0.20f, 0.12f), FontStyle.Bold);
            var points = Label(MakeRect("Points", inner, new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60, -170), new Vector2(-60, 56)),
                "Text", "", 36, TextAnchor.MiddleRight, brown, FontStyle.Bold);

            var close = MakeButton(frame, "CloseButton", "", new Vector2(120, 120), Red, 44);
            var closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(0.5f, 0.5f);
            closeRect.anchoredPosition = new Vector2(-40, -36);
            var closeSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/UI/Build/UI_CloseButton.png");
            if (closeSprite != null)
            {
                var closeImg = close.GetComponent<Image>();
                closeImg.sprite = closeSprite;
                closeImg.type = Image.Type.Simple;
                closeImg.preserveAspect = true;
                closeImg.color = Color.white;
            }
            else
            {
                close.GetComponentInChildren<TMP_Text>().text = "X";
            }

            var rows = new EventPrizeRowView[3];
            for (int i = 0; i < rows.Length; i++)
            {
                var row = MakeRect($"Prize{i + 1}", inner, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -250 - i * 196), new Vector2(-100, 176));
                var bg = AddImage(row, Color.white, raycast: false);
                var step = Label(MakeRect("Step", row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30, -18), new Vector2(300, 60)),
                    "Text", "Prize 1", 42, TextAnchor.MiddleLeft, brown, FontStyle.Bold);
                var barBg = MakeRect("Bar", row, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30, 40), new Vector2(380, 30));
                AddImage(barBg, new Color(0.82f, 0.78f, 0.7f), raycast: false);
                var fill = AddImage(Stretch("Fill", barBg, 3f), new Color(0.97f, 0.55f, 0.12f), raycast: false);
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                var progress = Label(MakeRect("Progress", row, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(424, 30), new Vector2(220, 50)),
                    "Text", "0/30", 34, TextAnchor.MiddleLeft, brown, FontStyle.Bold);

                var iconRect = MakeRect("RewardIcon", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(680, 0), new Vector2(130, 130));
                var icon = iconRect.gameObject.AddComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                var reward = Label(MakeRect("Reward", row, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), new Vector2(760, 0), new Vector2(-1000, -20)),
                    "Text", "150 coins", 34, TextAnchor.MiddleLeft, brown, FontStyle.Bold);

                var claim = MakeButton(row, "ClaimButton", "Claim", new Vector2(200, 96), Green, 38);
                var claimRect = (RectTransform)claim.transform;
                claimRect.anchorMin = claimRect.anchorMax = claimRect.pivot = new Vector2(1f, 0.5f);
                claimRect.anchoredPosition = new Vector2(-24, 0);
                var done = Label(MakeRect("Done", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24, 0), new Vector2(200, 80)),
                    "Text", "Claimed", 38, TextAnchor.MiddleCenter, new Color(0.25f, 0.55f, 0.2f), FontStyle.Bold);

                var view = row.gameObject.AddComponent<EventPrizeRowView>();
                Wire(view, ("background", bg), ("stepText", step), ("progressText", progress), ("progressFill", fill), ("rewardIcon", icon),
                    ("rewardText", reward), ("claimButton", claim), ("doneText", done));
                rows[i] = view;
            }

            var panel = container.gameObject.AddComponent<EventPanel>();
            Wire(panel, ("buttonRoot", buttonRect.gameObject), ("openButton", open), ("buttonTitle", bTitle), ("buttonTimer", bTimer),
                ("badge", badge.gameObject), ("panelRoot", root.gameObject), ("closeButton", close), ("titleText", title),
                ("descriptionText", desc), ("timerText", timer), ("pointsText", points),
                ("coinSprite", AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.CoinIconPath)),
                ("gemSprite", AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.GemIconPath)));
            WireArray(panel, "rows", rows);
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
            var collect = MakeButton(actions, "CollectButton", "Collect", new Vector2(220, 86), Green, out var collectLabel, 32);
            var finish = MakeButton(actions, "FinishNowButton", "Finish now", new Vector2(270, 86), new Color(0.95f, 0.65f, 0.15f), out var finishLabel, 26);
            var factoryGems = MakeButton(actions, "GemsButton", "Finish now\n1 gem", new Vector2(250, 86), new Color(0.3f, 0.6f, 0.85f), out var factoryGemsLabel, 26);

            var panel = container.gameObject.AddComponent<FactoryPanel>();
            Wire(panel, ("panelRoot", root.gameObject), ("titleText", title), ("queueTitleText", queueTitle),
                ("recipesParent", recipes), ("recipeTemplate", recipeView), ("collectButton", collect), ("collectLabel", collectLabel),
                ("finishNowButton", finish), ("finishNowLabel", finishLabel), ("gemsButton", factoryGems), ("gemsLabel", factoryGemsLabel),
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
            var statusRect = MakeRect("Status", growing, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0, -10), new Vector2(520, 70));
            var status = Label(statusRect, "Text", "", 36, TextAnchor.MiddleLeft, Ink);
            var barBg = MakeRect("ProgressBar", growing, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0, 30), new Vector2(520, 36));
            AddImage(barBg, new Color(0.82f, 0.78f, 0.7f), raycast: false);
            var fillRect = Stretch("Fill", barBg, 4f);
            var fill = AddImage(fillRect, Green, raycast: false);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0.3f;
            var action = MakeButton(growing, "ActionButton", "Finish now", new Vector2(250, 140), new Color(0.95f, 0.65f, 0.15f), out var actionLabel, 30);
            var actionRect = (RectTransform)action.transform;
            actionRect.anchorMin = actionRect.anchorMax = actionRect.pivot = new Vector2(1f, 0.5f);
            actionRect.anchoredPosition = new Vector2(-262, 0);
            var fieldGems = MakeButton(growing, "GemsButton", "Finish now\n1 gem", new Vector2(250, 140), new Color(0.3f, 0.6f, 0.85f), out var fieldGemsLabel, 30);
            var fieldGemsRect = (RectTransform)fieldGems.transform;
            fieldGemsRect.anchorMin = fieldGemsRect.anchorMax = fieldGemsRect.pivot = new Vector2(1f, 0.5f);
            fieldGemsRect.anchoredPosition = Vector2.zero;
            growing.gameObject.SetActive(false);

            var panel = container.gameObject.AddComponent<FieldPanel>();
            Wire(panel, ("panelRoot", root.gameObject), ("titleText", title), ("cropsView", crops.gameObject), ("cropsParent", crops),
                ("cropTemplate", cropView), ("growingView", growing.gameObject), ("statusText", status), ("progressFill", fill),
                ("actionButton", action), ("actionLabel", actionLabel), ("gemsButton", fieldGems), ("gemsLabel", fieldGemsLabel));
            root.gameObject.SetActive(false);
            return panel;
        }

        /// <summary>Developer mode: "DEV" button bottom-left + a panel of testing shortcuts (hidden in Google Play builds).</summary>
        private static DevPanel BuildDevPanel(Transform canvas)
        {
            var container = Stretch("DevPanel", canvas);
            var open = MakeButton(container, "DevButton", "DEV", new Vector2(110, 76), new Color(0.55f, 0.2f, 0.6f), 30);
            var openRect = (RectTransform)open.transform;
            openRect.anchorMin = openRect.anchorMax = openRect.pivot = new Vector2(0f, 0f);
            openRect.anchoredPosition = new Vector2(30, 30); // bottom-left corner: clear of every window's close button

            var root = Stretch("Root", container);
            AddImage(root, Dim, rounded: false);
            var window = MakeRect("Window", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640, 1050));
            AddImage(window, PanelColor);
            VColumn(window, 8, TextAnchor.UpperCenter, 20);
            var title = Label(window, "Title", "Developer mode (test builds only)", 34, TextAnchor.MiddleCenter, Ink, FontStyle.Bold);
            Prefer(title, 560, 60);

            Button Row(string name, string label, Color color) => MakeButton(window, name, label, new Vector2(560, 74), color, 26);
            var coins = Row("CoinsButton", "+10,000 coins  +100 gems", new Color(0.95f, 0.65f, 0.15f));
            var levelUp = Row("LevelUpButton", "Level up (+1)", Blue);
            var unlockAll = Row("UnlockAllButton", $"Unlock everything (level {DevTools.UnlockAllLevel})", Blue);
            var fillBarn = Row("FillBarnButton", "Max barn + 10 of every item", new Color(0.62f, 0.38f, 0.25f));
            var finish = Row("FinishTimersButton", "Finish all timers + call train/truck", Green);
            var land = Row("UnlockLandButton", "Unlock all land", Green);
            var dailyAgain = Row("DailyAgainButton", "Daily reward again", new Color(0.55f, 0.35f, 0.7f));
            var startEvent = Row("DevEventButton", "Start weekend event now", new Color(0.95f, 0.5f, 0.15f));
            var findItem = Row("DevCollectionButton", "Find a collection item", new Color(0.2f, 0.6f, 0.6f));
            var nextSeason = Row("DevSeasonButton", "Next season (test)", new Color(0.35f, 0.55f, 0.2f));
            var close = Row("CloseButton", "Close", Slate);

            var panel = container.gameObject.AddComponent<DevPanel>();
            Wire(panel, ("openButton", open), ("panelRoot", root.gameObject), ("closeButton", close), ("coinsButton", coins),
                ("levelUpButton", levelUp), ("unlockAllButton", unlockAll), ("fillBarnButton", fillBarn),
                ("finishTimersButton", finish), ("unlockLandButton", land), ("dailyAgainButton", dailyAgain), ("eventButton", startEvent),
                ("collectionButton", findItem), ("seasonButton", nextSeason));
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

        /// <summary>
        /// Collections: a round chest button under the menu button (badge = sets ready to complete) and the window with
        /// one row per collection: name, where it's found, five item slots, reward, Complete.
        /// </summary>
        private static CollectionsPanel BuildCollectionsPanel(Transform canvas, RectTransform safe)
        {
            var outline = AssetDatabase.LoadAssetAtPath<Material>(ProjectSetup.UIFontOutlineMaterialPath);
            var brown = new Color(0.40f, 0.24f, 0.10f);

            var open = MakeButton(safe, "CollectionsButton", "", new Vector2(104, 104), new Color(0.62f, 0.38f, 0.22f), 30);
            var openRect = (RectTransform)open.transform;
            openRect.anchorMin = openRect.anchorMax = openRect.pivot = new Vector2(1f, 1f);
            openRect.anchoredPosition = new Vector2(-24, -150);
            var openImg = open.GetComponent<Image>();
            openImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.CirclePath);
            openImg.type = Image.Type.Simple;
            var chest = MakeRect("Icon", openRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70, 70));
            var chestImg = chest.gameObject.AddComponent<Image>();
            chestImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.ChestIconPath);
            chestImg.preserveAspect = true;
            chestImg.raycastTarget = false;
            var badge = MakeRect("Badge", openRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-8, -8), new Vector2(46, 46));
            var badgeImg = badge.gameObject.AddComponent<Image>();
            badgeImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.CirclePath);
            badgeImg.color = new Color(0.90f, 0.17f, 0.14f);
            badgeImg.raycastTarget = false;
            var badgeText = Label(badge, "Text", "1", 28, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            if (outline != null) badgeText.fontSharedMaterial = outline;
            badge.gameObject.SetActive(false);

            var container = Stretch("CollectionsPanel", canvas);
            var root = Stretch("Root", container);
            AddImage(root, Dim, rounded: false);
            var window = MakeRect("Window", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1640, 930));
            AddImage(window, new Color(0.62f, 0.38f, 0.22f));
            var inner = Stretch("Inner", window, 14f);
            AddImage(inner, new Color(1f, 0.96f, 0.86f));
            Label(MakeRect("Title", inner, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(40, -14), new Vector2(-300, 80)),
                "Text", "Collections", 58, TextAnchor.MiddleLeft, brown, FontStyle.Bold);
            Label(MakeRect("Subtitle", inner, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(420, -14), new Vector2(-600, 80)),
                "Text", "Rare finds turn up now and then. Find all five to complete a set.", 30, TextAnchor.MiddleLeft, new Color(0.45f, 0.42f, 0.38f));
            var close = MakeButton(window, "CloseButton", "", new Vector2(110, 110), Red, 44);
            var closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(0.5f, 0.5f);
            closeRect.anchoredPosition = new Vector2(-34, -34);
            var closeSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/UI/Build/UI_CloseButton.png");
            if (closeSprite != null)
            {
                var closeImg = close.GetComponent<Image>();
                closeImg.sprite = closeSprite;
                closeImg.type = Image.Type.Simple;
                closeImg.preserveAspect = true;
                closeImg.color = Color.white;
            }
            else
            {
                close.GetComponentInChildren<TMP_Text>().text = "X";
            }

            var rows = new CollectionRowView[3];
            for (int i = 0; i < rows.Length; i++)
            {
                var row = MakeRect($"Collection{i + 1}", inner, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -108 - i * 266), new Vector2(-60, 252));
                var bg = AddImage(row, Color.white, raycast: false);
                var name = Label(MakeRect("Name", row, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(22, -6), new Vector2(-300, 50)),
                    "Text", "Meadow Treasures", 36, TextAnchor.MiddleLeft, brown, FontStyle.Bold);
                var source = Label(MakeRect("Source", row, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(22, -54), new Vector2(-300, 36)),
                    "Text", "", 25, TextAnchor.MiddleLeft, new Color(0.45f, 0.42f, 0.38f));
                var icons = new Image[5];
                var counts = new TMP_Text[5];
                var names = new TMP_Text[5];
                for (int s = 0; s < 5; s++)
                {
                    var slot = MakeRect($"Slot{s + 1}", row, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(22 + s * 228, 12), new Vector2(214, 148));
                    AddImage(slot, new Color(0.95f, 0.91f, 0.82f), raycast: false);
                    var iconRect = MakeRect("Icon", slot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -6), new Vector2(96, 96));
                    icons[s] = iconRect.gameObject.AddComponent<Image>();
                    icons[s].preserveAspect = true;
                    icons[s].raycastTarget = false;
                    counts[s] = Label(MakeRect("Count", slot, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-8, -6), new Vector2(60, 34)),
                        "Text", "", 26, TextAnchor.MiddleRight, brown, FontStyle.Bold);
                    names[s] = Label(MakeRect("Name", slot, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 6), new Vector2(-10, 36)),
                        "Text", "?", 22, TextAnchor.MiddleCenter, brown, FontStyle.Bold);
                    names[s].enableAutoSizing = true;
                    names[s].fontSizeMin = 16;
                    names[s].fontSizeMax = 22;
                    names[s].textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                }

                var reward = Label(MakeRect("Reward", row, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-22, -20), new Vector2(330, 70)),
                    "Text", "Reward: 150 coins + 3 gems", 26, TextAnchor.MiddleCenter, new Color(0.2f, 0.45f, 0.75f), FontStyle.Bold);
                var complete = MakeButton(row, "CompleteButton", "Complete", new Vector2(300, 100), Green, 36);
                var completeRect = (RectTransform)complete.transform;
                completeRect.anchorMin = completeRect.anchorMax = completeRect.pivot = new Vector2(1f, 0f);
                completeRect.anchoredPosition = new Vector2(-36, 24);

                var view = row.gameObject.AddComponent<CollectionRowView>();
                Wire(view, ("background", bg), ("nameText", name), ("sourceText", source), ("rewardText", reward), ("completeButton", complete));
                WireArray(view, "slotIcons", icons);
                WireArray(view, "slotCounts", counts);
                WireArray(view, "slotNames", names);
                rows[i] = view;
            }

            var panel = container.gameObject.AddComponent<CollectionsPanel>();
            Wire(panel, ("panelRoot", root.gameObject), ("openButton", open), ("closeButton", close), ("badge", badge.gameObject), ("badgeText", badgeText));
            WireArray(panel, "rows", rows);
            root.gameObject.SetActive(false);
            return panel;
        }

        /// <summary>
        /// Landscape tool bar (v0.36.0): title, hint, Flip corner (roads), up to five style slots, Eraser, Undo, the red
        /// button (remove the pending road / cancel) and the green button (build the road / done).
        /// </summary>
        private static LandscapePanel BuildLandscapePanel(RectTransform safe, params GameObject[] hide)
        {
            const int slotCount = 5;
            var outline = AssetDatabase.LoadAssetAtPath<Material>(ProjectSetup.UIFontOutlineMaterialPath);
            var brown = new Color(0.40f, 0.24f, 0.10f);
            var container = Stretch("LandscapePanel", safe);
            var root = MakeRect("Root", container, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 24), new Vector2(1640, 250));
            AddImage(root, new Color(0.30f, 0.17f, 0.07f));
            var inner = Stretch("Inner", root, 8f);
            AddImage(inner, new Color(1f, 0.96f, 0.86f));
            var title = Label(MakeRect("Title", inner, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24, -8), new Vector2(420, 54)),
                "Text", "Roads & paths", 40, TextAnchor.MiddleLeft, brown, FontStyle.Bold);
            var hint = Label(MakeRect("Hint", inner, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(460, -8), new Vector2(-720, 54)),
                "Text", "", 26, TextAnchor.MiddleLeft, new Color(0.45f, 0.42f, 0.38f));
            hint.enableAutoSizing = true;
            hint.fontSizeMin = 18;
            hint.fontSizeMax = 26;

            var flip = MakeButton(inner, "FlipButton", "Flip corner", new Vector2(220, 56), Blue, 28);
            var flipRect = (RectTransform)flip.transform;
            flipRect.anchorMin = flipRect.anchorMax = flipRect.pivot = new Vector2(1f, 1f);
            flipRect.anchoredPosition = new Vector2(-18, -8);

            var sprites = PlaceholderArtGenerator.LoadLandscapeSprites();
            Image Slot(string name, float x, string label, out Button button, out Image picture, out TMP_Text text)
            {
                var slot = MakeRect(name, inner, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(x, 14), new Vector2(156, 164));
                var frame = AddImage(slot, Color.white);
                button = slot.gameObject.AddComponent<Button>();
                button.targetGraphic = frame;
                var pic = MakeRect("Tile", slot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -14), new Vector2(132, 66));
                picture = pic.gameObject.AddComponent<Image>();
                picture.preserveAspect = true;
                picture.raycastTarget = false;
                text = Label(MakeRect("Name", slot, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 10), new Vector2(-10, 60)),
                    "Text", label, 26, TextAnchor.MiddleCenter, brown, FontStyle.Bold);
                text.enableAutoSizing = true;
                text.fontSizeMin = 18;
                text.fontSizeMax = 26;
                return frame;
            }

            var slots = new Button[slotCount];
            var frames = new Image[slotCount];
            var pictures = new Image[slotCount];
            var names = new TMP_Text[slotCount];
            for (int i = 0; i < slotCount; i++)
                frames[i] = Slot($"Slot{i + 1}", 24 + i * 168, "Style", out slots[i], out pictures[i], out names[i]);
            var eraserFrame = Slot("EraserSlot", 24 + slotCount * 168 + 10, "Eraser", out var eraser, out var eraserPic, out _);
            eraserPic.sprite = sprites[(int)TownBuilder.Domain.Terrain.Grass] as Sprite;
            var cross = Label(MakeRect("Cross", eraserPic.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80, 66)),
                "Text", "X", 54, TextAnchor.MiddleCenter, new Color(0.86f, 0.33f, 0.29f), FontStyle.Bold);
            if (outline != null) cross.fontSharedMaterial = outline;

            var undo = MakeButton(inner, "UndoButton", "Undo", new Vector2(150, 150), Slate, 36);
            var undoRect = (RectTransform)undo.transform;
            undoRect.anchorMin = undoRect.anchorMax = undoRect.pivot = new Vector2(0f, 0f);
            undoRect.anchoredPosition = new Vector2(24 + (slotCount + 1) * 168 + 24, 18);

            var cancel = MakeButton(inner, "CancelButton", "X", new Vector2(150, 150), Red, out var cancelLabel, 60);
            var cancelRect = (RectTransform)cancel.transform;
            cancelRect.anchorMin = cancelRect.anchorMax = cancelRect.pivot = new Vector2(1f, 0f);
            cancelRect.anchoredPosition = new Vector2(-196, 18);
            var done = MakeButton(inner, "DoneButton", "Done", new Vector2(170, 150), Green, out var doneLabel, 40);
            var doneRect = (RectTransform)done.transform;
            doneRect.anchorMin = doneRect.anchorMax = doneRect.pivot = new Vector2(1f, 0f);
            doneRect.anchoredPosition = new Vector2(-18, 18);
            if (outline != null)
            {
                cancelLabel.fontSharedMaterial = outline;
                doneLabel.fontSharedMaterial = outline;
                undo.GetComponentInChildren<TMP_Text>().fontSharedMaterial = outline;
                flip.GetComponentInChildren<TMP_Text>().fontSharedMaterial = outline;
            }

            var panel = container.gameObject.AddComponent<LandscapePanel>();
            Wire(panel, ("panelRoot", root.gameObject), ("titleText", title), ("hintText", hint), ("eraserButton", eraser), ("eraserFrame", eraserFrame),
                ("undoButton", undo), ("flipButton", flip), ("cancelButton", cancel), ("cancelLabel", cancelLabel), ("doneButton", done), ("doneLabel", doneLabel));
            WireArray(panel, "slots", slots);
            WireArray(panel, "slotFrames", frames);
            WireArray(panel, "slotPictures", pictures);
            WireArray(panel, "slotNames", names);
            WireArray(panel, "terrainSprites", sprites);
            WireArray(panel, "hideWhilePainting", hide);
            root.gameObject.SetActive(false);
            return panel;
        }

        /// <summary>Home panel: name, happiness bar + %, what it earns, a decoration hint, Collect.</summary>
        private static HomePanel BuildHomePanel(RectTransform safe)
        {
            var container = Stretch("HomePanel", safe);
            var root = MakeRect("Root", container, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 196), new Vector2(1240, 270));
            AddImage(root, PanelColor);
            var title = Label(MakeRect("Title", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24, -10), new Vector2(500, 54)),
                "Text", "Cottage", 38, TextAnchor.MiddleLeft, Ink, FontStyle.Bold);
            var happy = Label(MakeRect("Happiness", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24, -72), new Vector2(300, 48)),
                "Text", "Happiness 0%", 32, TextAnchor.MiddleLeft, Ink, FontStyle.Bold);
            var barBg = MakeRect("Bar", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(330, -82), new Vector2(560, 30));
            AddImage(barBg, new Color(0.82f, 0.78f, 0.7f), raycast: false);
            var fill = AddImage(Stretch("Fill", barBg, 3f), Green, raycast: false);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            var info = Label(MakeRect("Info", root, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(24, 18), new Vector2(-340, 120)),
                "Text", "", 27, TextAnchor.UpperLeft, new Color(0.40f, 0.36f, 0.32f));
            var collect = MakeButton(root, "CollectButton", "Collect", new Vector2(260, 150), new Color(0.95f, 0.72f, 0.15f), out var collectLabel, 32);
            var collectRect = (RectTransform)collect.transform;
            collectRect.anchorMin = collectRect.anchorMax = collectRect.pivot = new Vector2(1f, 0.5f);
            collectRect.anchoredPosition = new Vector2(-24, -10);

            var panel = container.gameObject.AddComponent<HomePanel>();
            Wire(panel, ("panelRoot", root.gameObject), ("titleText", title), ("happinessText", happy), ("happinessFill", fill), ("infoText", info),
                ("collectButton", collect), ("collectLabel", collectLabel));
            root.gameObject.SetActive(false);
            return panel;
        }

        /// <summary>Truck Depot panel: status line, three load cards (two item lines each), Send / Call back buttons.</summary>
        private static TruckPanel BuildTruckPanel(RectTransform safe)
        {
            var container = Stretch("TruckPanel", safe);
            var root = MakeRect("Root", container, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 196), new Vector2(1640, 360));
            AddImage(root, PanelColor);

            Label(MakeRect("Title", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24, -10), new Vector2(400, 54)),
                "Text", "Truck Depot", 38, TextAnchor.MiddleLeft, Ink, FontStyle.Bold);
            var status = Label(MakeRect("Status", root, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(150, -10), new Vector2(-700, 54)),
                "Text", "", 28, TextAnchor.MiddleLeft, Ink, FontStyle.Bold);

            var row = MakeRect("Loads", root, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            row.offsetMin = new Vector2(24, 16);
            row.offsetMax = new Vector2(24 + 3 * 400 + 2 * 14, -72);
            HRow(row, 14, TextAnchor.MiddleLeft);

            var loads = new TruckLoadView[3];
            for (int i = 0; i < loads.Length; i++)
            {
                var card = MakeRect($"Load{i + 1}", row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 260));
                Prefer(card, 400, 260);
                var bg = AddImage(card, Color.white, raycast: false);
                var icons = new Image[2];
                var texts = new TMP_Text[2];
                for (int l = 0; l < 2; l++)
                {
                    var iconRect = MakeRect($"Icon{l + 1}", card, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14, -12 - l * 66), new Vector2(60, 60));
                    icons[l] = iconRect.gameObject.AddComponent<Image>();
                    icons[l].preserveAspect = true;
                    icons[l].raycastTarget = false;
                    texts[l] = Label(MakeRect($"Line{l + 1}", card, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(84, -12 - l * 66), new Vector2(-96, 60)),
                        "Text", "", 26, TextAnchor.MiddleLeft, Ink, FontStyle.Bold);
                }

                var reward = Label(MakeRect("Reward", card, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(14, 10), new Vector2(-200, 100)),
                    "Text", "", 22, TextAnchor.MiddleLeft, new Color(0.45f, 0.42f, 0.38f));
                var loadButton = MakeButton(card, "LoadButton", "Load", new Vector2(160, 70), Green, 30);
                var loadRect = (RectTransform)loadButton.transform;
                loadRect.anchorMin = loadRect.anchorMax = loadRect.pivot = new Vector2(1f, 0f);
                loadRect.anchoredPosition = new Vector2(-14, 14);
                var loaded = Label(MakeRect("Loaded", card, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-14, 14), new Vector2(160, 70)),
                    "Text", "Loaded", 30, TextAnchor.MiddleCenter, new Color(0.25f, 0.55f, 0.2f), FontStyle.Bold);

                var view = card.gameObject.AddComponent<TruckLoadView>();
                Wire(view, ("background", bg), ("rewardText", reward), ("loadButton", loadButton), ("loadedText", loaded));
                WireArray(view, "lineIcons", icons);
                WireArray(view, "lineTexts", texts);
                loads[i] = view;
            }

            var send = MakeButton(root, "SendButton", "Send truck", new Vector2(320, 130), Blue, out var sendLabel, 32);
            var sendRect = (RectTransform)send.transform;
            sendRect.anchorMin = sendRect.anchorMax = sendRect.pivot = new Vector2(1f, 0.5f);
            sendRect.anchoredPosition = new Vector2(-30, -30);
            var call = MakeButton(root, "CallButton", "Call back", new Vector2(300, 130), new Color(0.95f, 0.65f, 0.15f), out var callLabel, 30);
            var callRect = (RectTransform)call.transform;
            callRect.anchorMin = callRect.anchorMax = callRect.pivot = new Vector2(0.5f, 0.5f);
            callRect.anchoredPosition = new Vector2(-170, -40);
            var gems = MakeButton(root, "GemsButton", "Call back\n1 gem", new Vector2(300, 130), new Color(0.3f, 0.6f, 0.85f), out var gemsLabel, 30);
            var gemsRect = (RectTransform)gems.transform;
            gemsRect.anchorMin = gemsRect.anchorMax = gemsRect.pivot = new Vector2(0.5f, 0.5f);
            gemsRect.anchoredPosition = new Vector2(170, -40);

            var panel = container.gameObject.AddComponent<TruckPanel>();
            Wire(panel, ("panelRoot", root.gameObject), ("statusText", status), ("loadsRow", row.gameObject),
                ("sendButton", send), ("sendLabel", sendLabel), ("callButton", call), ("callLabel", callLabel),
                ("gemsButton", gems), ("gemsLabel", gemsLabel));
            WireArray(panel, "loads", loads);
            root.gameObject.SetActive(false);
            return panel;
        }

        private static TasksPanel BuildTasksPanel(RectTransform safe)
        {
            const float width = 470f, rowHeight = 96f;
            var outline = AssetDatabase.LoadAssetAtPath<Material>(ProjectSetup.UIFontOutlineMaterialPath);

            // v0.35.2: closed by default; a round Tasks button (left, mirrors the Collections chest on the right) opens it.
            var container = MakeRect("TasksPanel", safe, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24, -150),
                new Vector2(104 + 14 + width, 52 + 3 * (rowHeight + 8) + 12));
            var open = MakeButton(container, "TasksButton", "", new Vector2(104, 104), new Color(0.30f, 0.62f, 0.35f), 30);
            var openRect = (RectTransform)open.transform;
            openRect.anchorMin = openRect.anchorMax = openRect.pivot = new Vector2(0f, 1f);
            openRect.anchoredPosition = Vector2.zero;
            var openImg = open.GetComponent<Image>();
            openImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.CirclePath);
            openImg.type = Image.Type.Simple;
            var icon = MakeRect("Icon", openRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(68, 68));
            var iconImg = icon.gameObject.AddComponent<Image>();
            iconImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.TasksIconPath);
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            var badge = MakeRect("Badge", openRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-8, -8), new Vector2(46, 46));
            var badgeImg = badge.gameObject.AddComponent<Image>();
            badgeImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.CirclePath);
            badgeImg.color = new Color(0.90f, 0.17f, 0.14f);
            badgeImg.raycastTarget = false;
            var badgeText = Label(badge, "Text", "1", 28, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            if (outline != null) badgeText.fontSharedMaterial = outline;
            badge.gameObject.SetActive(false);

            var root = MakeRect("Root", container, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(118, 0), new Vector2(width, 0));
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
            Wire(panel, ("panelRoot", root.gameObject), ("openButton", open), ("badge", badge.gameObject), ("badgeText", badgeText));
            WireArray(panel, "rows", rows);
            root.gameObject.SetActive(false);
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

        /// <summary>
        /// Top-bar pill: a dark rounded bar with a big icon overlapping its left end and the number right-aligned
        /// (white with an outline). The layout reserves room for the icon sticking out on the left.
        /// </summary>
        private static TMP_Text Pill(RectTransform parent, string name, float width, string iconPath)
        {
            var slot = MakeRect(name + "Slot", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width + 40, 110));
            Prefer(slot, width + 40, 110);
            var rt = MakeRect(name, slot, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(width, 70));
            AddImage(rt, PillColor);
            var text = Label(rt, "Text", "-", 46, TextAnchor.MiddleRight, Color.white, FontStyle.Bold);
            var outline = AssetDatabase.LoadAssetAtPath<Material>(ProjectSetup.UIFontOutlineMaterialPath);
            if (outline != null) text.fontSharedMaterial = outline;
            var textRect = (RectTransform)text.transform;
            textRect.offsetMin = new Vector2(70, 0);
            textRect.offsetMax = new Vector2(-22, 0);

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            if (sprite != null)
            {
                var iconRect = MakeRect("Icon", rt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(6, 0), new Vector2(108, 108));
                var icon = iconRect.gameObject.AddComponent<Image>();
                icon.sprite = sprite;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
            }

            return text;
        }

        /// <summary>Level star with the level number in it, overlapping a blue XP bar (with "x/y XP" on it).</summary>
        private static TMP_Text LevelBadge(RectTransform topBar, out Image xpFill, out TMP_Text xpText, out RectTransform pill)
        {
            var outline = AssetDatabase.LoadAssetAtPath<Material>(ProjectSetup.UIFontOutlineMaterialPath);
            pill = MakeRect("LevelPill", topBar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(64, 0), new Vector2(330, 62));
            AddImage(pill, PillColor);
            var fillArea = Stretch("Bar", pill, 7f);
            AddImage(fillArea, new Color(0.10f, 0.22f, 0.38f, 0.9f), raycast: false);
            xpFill = AddImage(Stretch("Fill", fillArea, 0f), new Color(0.20f, 0.66f, 1f), raycast: false);
            xpFill.type = Image.Type.Filled;
            xpFill.fillMethod = Image.FillMethod.Horizontal;
            xpFill.fillAmount = 0.4f;
            var shine = MakeRect("Shine", xpFill.rectTransform, new Vector2(0f, 0.55f), new Vector2(1f, 0.9f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            AddImage(shine, new Color(1f, 1f, 1f, 0.25f), rounded: false, raycast: false);
            xpText = Label(MakeRect("XpText", pill, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(30, 0), new Vector2(-70, 0)),
                "Text", "0/15 XP", 30, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            if (outline != null) xpText.fontSharedMaterial = outline;

            var star = MakeRect("Star", pill, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-6, 2), new Vector2(124, 124));
            var starImg = star.gameObject.AddComponent<Image>();
            starImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.UiArtFolder + "/PH_Icon_Level.png");
            starImg.preserveAspect = true;
            starImg.raycastTarget = false;
            var level = Label(MakeRect("Level", star, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0, -6), Vector2.zero),
                "Text", "1", 50, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            if (outline != null) level.fontSharedMaterial = outline;
            return level;
        }

        private static BuildingShopPanel BuildShop(Transform canvas)
        {
            var container = Stretch("BuildingShopPanel", canvas);
            var root = Stretch("Root", container);
            AddImage(root, Dim, rounded: false);

            // --- Screen 2: one category's buildings, in the same full window (sign = category name).
            var brown = new Color(0.40f, 0.24f, 0.10f);
            var window = BuildShopFrame(root, "Window", "HOUSES", out var listInner, out var close, out var listTitle);
            var back = MakeButton(window, "BackButton", "<", new Vector2(120, 120), new Color(0.18f, 0.52f, 0.95f), 64);
            var backRect = (RectTransform)back.transform;
            backRect.anchorMin = backRect.anchorMax = new Vector2(0f, 1f);
            backRect.pivot = new Vector2(0.5f, 0.5f);
            backRect.anchoredPosition = new Vector2(40, -18);
            var backImg = back.GetComponent<Image>();
            backImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.CirclePath);
            backImg.type = Image.Type.Simple;
            var backOutline = AssetDatabase.LoadAssetAtPath<Material>(ProjectSetup.UIFontOutlineMaterialPath);
            if (backOutline != null) back.GetComponentInChildren<TMP_Text>().fontSharedMaterial = backOutline;

            var viewport = MakeRect("Viewport", listInner, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            viewport.offsetMin = new Vector2(24, 20);
            viewport.offsetMax = new Vector2(-24, -86);
            var items = HorizontalScrollList(viewport, "Items", 22);
            items.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(6, 6, 0, 0);

            // Building card template (cloned per building by BuildingShopPanel).
            var template = MakeRect("ItemTemplate", items, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 700));
            Prefer(template, 400, 700);
            var bg = AddImage(template, new Color(1f, 0.985f, 0.94f), raycast: false);
            var cardShadow = template.gameObject.AddComponent<Shadow>();
            cardShadow.effectColor = new Color(0.45f, 0.30f, 0.15f, 0.35f);
            cardShadow.effectDistance = new Vector2(0f, -7f);

            var nameText = Label(MakeRect("Name", template, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -14), new Vector2(-30, 96)),
                "Text", "Name", 40, TextAnchor.MiddleCenter, brown, FontStyle.Bold);
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = 26;
            nameText.fontSizeMax = 40;
            var countText = Label(MakeRect("Count", template, new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(24, -116), new Vector2(-24, 48)),
                "Text", "0/1", 32, TextAnchor.MiddleLeft, brown, FontStyle.Bold);
            var bonusText = Label(MakeRect("Bonus", template, new Vector2(0.4f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24, -116), new Vector2(0, 48)),
                "Text", "", 30, TextAnchor.MiddleRight, new Color(0.2f, 0.5f, 0.2f), FontStyle.Bold);
            bonusText.enableAutoSizing = true;
            bonusText.fontSizeMin = 20;
            bonusText.fontSizeMax = 30;
            bonusText.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            var line = MakeRect("Line", template, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -170), new Vector2(-40, 3));
            AddImage(line, new Color(0.85f, 0.78f, 0.64f), rounded: false, raycast: false);

            var iconRect = MakeRect("Icon", template, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -186), new Vector2(340, 330));
            var icon = iconRect.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var buy = MakeButton(template, "BuyButton", "", new Vector2(320, 110), Green, 44);
            var buyRect = (RectTransform)buy.transform;
            buyRect.anchorMin = buyRect.anchorMax = buyRect.pivot = new Vector2(0.5f, 0f);
            buyRect.anchoredPosition = new Vector2(0, 30);
            var coinRect = MakeRect("Coin", buyRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(64, 0), new Vector2(76, 76));
            var coin = coinRect.gameObject.AddComponent<Image>();
            coin.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.CoinIconPath);
            coin.preserveAspect = true;
            coin.raycastTarget = false;
            var priceText = buy.GetComponentInChildren<TMP_Text>();
            var priceRect = (RectTransform)priceText.transform;
            priceRect.offsetMin = new Vector2(100, 0);
            priceRect.offsetMax = new Vector2(-16, 0);
            priceText.text = "450";
            priceText.alignment = TMPro.TextAlignmentOptions.Center;
            if (backOutline != null) priceText.fontSharedMaterial = backOutline;
            var statusText = Label(MakeRect("Status", template, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(-30, 110)),
                "Text", "Built", 38, TextAnchor.MiddleCenter, brown, FontStyle.Bold);

            var itemView = template.gameObject.AddComponent<ShopItemView>();
            Wire(itemView, ("button", buy), ("icon", icon), ("nameText", nameText), ("priceText", priceText), ("countText", countText),
                ("bonusText", bonusText), ("statusText", statusText), ("background", bg));
            template.gameObject.SetActive(false);

            var cards = BuildShopCardsScreen(root, out var cardsParent, out var cardTemplate, out var cardsClose);

            var panel = container.gameObject.AddComponent<BuildingShopPanel>();
            Wire(panel, ("panelRoot", root.gameObject), ("itemsParent", items), ("itemTemplate", itemView), ("closeButton", close),
                ("listTitle", listTitle), ("listScreen", window.gameObject), ("backButton", back),
                ("cardsScreen", cards), ("cardsParent", cardsParent), ("cardTemplate", cardTemplate), ("cardsCloseButton", cardsClose),
                ("scroll", viewport.GetComponent<ScrollRect>()));
            WireArray(panel, "cardRibbons", RibbonArtNames.Select(n => (Object)AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Game/Art/UI/Build/UI_Ribbon_{n}.png")).ToArray());
            WireArray(panel, "cardPictures", CardArtNames.Select(n => (Object)AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Game/Art/UI/Build/UI_Card_{n}.png")).ToArray());
            root.gameObject.SetActive(false);
            return panel;
        }

        /// <summary>
        /// Build screen 1 (v0.27.4, owner's design): a blue-framed cream window with a striped awning, the wooden BUILD
        /// sign, a round close button, and one card per category (ribbon, picture, "N buildings", badge), leaves in the
        /// bottom corners. Art: Art/UI/Build (AI images, see ArtPrompts.md) + awning/badge drawn in code.
        /// </summary>
        /// <summary>
        /// The Build windows' shared frame (our own look): wooden frame, green-and-white awning, wooden title sign, leaves
        /// in the bottom corners, round close button. Returns the frame; <paramref name="inner"/> is the cream panel.
        /// </summary>
        private static RectTransform BuildShopFrame(RectTransform root, string windowName, string title, out RectTransform inner, out Button close,
            out TMP_Text signText, Vector2? size = null)
        {
            Sprite Ui(string file) => AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Game/Art/UI/Build/{file}.png");
            var outline = AssetDatabase.LoadAssetAtPath<Material>(ProjectSetup.UIFontOutlineMaterialPath);

            // Wooden frame: dark outline, plank texture, a darker inner edge, then the cream panel.
            var frame = MakeRect(windowName, root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40),
                size ?? new Vector2(1820, 930));
            AddImage(frame, new Color(0.30f, 0.17f, 0.07f));
            var wood = Stretch("Wood", frame, 5f);
            var woodImg = wood.gameObject.AddComponent<Image>();
            woodImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.WoodPath);
            woodImg.type = Image.Type.Tiled;
            woodImg.raycastTarget = false;
            if (woodImg.sprite == null) woodImg.color = new Color(0.72f, 0.46f, 0.22f);
            var rim = Stretch("InnerEdge", frame, 26f);
            AddImage(rim, new Color(0.36f, 0.21f, 0.09f), raycast: false);
            inner = Stretch("Inner", frame, 31f);
            AddImage(inner, new Color(1f, 0.96f, 0.86f));

            var awning = MakeRect("Awning", frame, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -26), new Vector2(-62, 72));
            var awningImg = awning.gameObject.AddComponent<Image>();
            awningImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.AwningPath);
            awningImg.type = Image.Type.Tiled;
            awningImg.raycastTarget = false;

            // Leaves in the bottom corners (the right one mirrored).
            var leaves = Ui("UI_CornerLeaves");
            foreach (var (name, anchor, pos, flip) in new[] { ("LeavesLeft", new Vector2(0f, 0f), new Vector2(36, 34), 1f), ("LeavesRight", new Vector2(1f, 0f), new Vector2(-36, 34), -1f) })
            {
                var r = MakeRect(name, frame, anchor, anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(160, 139));
                var img = r.gameObject.AddComponent<Image>();
                img.sprite = leaves;
                img.preserveAspect = true;
                img.raycastTarget = false;
                img.enabled = leaves != null;
                r.localScale = new Vector3(flip, 1f, 1f);
            }

            // Wooden sign with the title, poking out above the window.
            var sign = MakeRect("Sign", frame, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -4), new Vector2(680, 164));
            var signImg = sign.gameObject.AddComponent<Image>();
            signImg.sprite = Ui("UI_BuildSign");
            signImg.preserveAspect = true;
            signImg.raycastTarget = false;
            if (signImg.sprite == null) signImg.color = new Color(0.85f, 0.5f, 0.2f);
            signText = Label(MakeRect("Title", sign, new Vector2(0.2f, 0f), new Vector2(0.8f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, 4), Vector2.zero),
                "Text", title, 84, TextAnchor.MiddleCenter, new Color(1f, 0.95f, 0.85f), FontStyle.Bold);
            if (outline != null) signText.fontSharedMaterial = outline;
            signText.textWrappingMode = TMPro.TextWrappingModes.NoWrap; // long names ("DECORATIONS") shrink instead of wrapping
            signText.enableAutoSizing = true;
            signText.fontSizeMin = 40;
            signText.fontSizeMax = 84;


            close = MakeButton(frame, "CloseButton", "", new Vector2(130, 130), Red, 44);
            var closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(0.5f, 0.5f);
            closeRect.anchoredPosition = new Vector2(-40, -18);
            var closeSprite = Ui("UI_CloseButton");
            if (closeSprite != null)
            {
                var closeImg = close.GetComponent<Image>();
                closeImg.sprite = closeSprite;
                closeImg.type = Image.Type.Simple;
                closeImg.preserveAspect = true;
                closeImg.color = Color.white;
            }
            else
            {
                close.GetComponentInChildren<TMP_Text>().text = "X";
            }

            return frame;
        }

        private static GameObject BuildShopCardsScreen(RectTransform root, out Transform cardsParent, out ShopCategoryCardView cardTemplate,
            out Button close)
        {
            Sprite Ui(string file) => AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Game/Art/UI/Build/{file}.png");
            var outline = AssetDatabase.LoadAssetAtPath<Material>(ProjectSetup.UIFontOutlineMaterialPath);
            var brown = new Color(0.40f, 0.24f, 0.10f);

            var frame = BuildShopFrame(root, "CardsWindow", "BUILD", out var inner, out close, out _);

            Label(MakeRect("Subtitle", inner, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -96), new Vector2(-200, 56)),
                "Text", "What would you like to build?", 42, TextAnchor.MiddleCenter, brown, FontStyle.Bold);

            var row = MakeRect("Cards", inner, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 56), new Vector2(1740, 590));
            HRow(row, 20, TextAnchor.MiddleCenter);
            cardsParent = row;

            // Card template (cloned per category by BuildingShopPanel).
            var card = MakeRect("CardTemplate", row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(332, 570));
            Prefer(card, 332, 570);
            var cardBg = AddImage(card, new Color(1f, 0.985f, 0.94f));
            var shadow = card.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.45f, 0.30f, 0.15f, 0.35f);
            shadow.effectDistance = new Vector2(0f, -7f);
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = cardBg;

            var pictureRect = MakeRect("Picture", card, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -62), new Vector2(306, 324));
            var picture = pictureRect.gameObject.AddComponent<Image>();
            picture.preserveAspect = true;
            picture.raycastTarget = false;

            var lockRect = Stretch("Lock", pictureRect, 6f);
            AddImage(lockRect, new Color(0.1f, 0.08f, 0.06f, 0.5f), raycast: false);
            var lockIconRect = MakeRect("Icon", lockRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110, 110));
            var lockIcon = lockIconRect.gameObject.AddComponent<Image>();
            lockIcon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.LockIconPath);
            lockIcon.preserveAspect = true;
            lockIcon.raycastTarget = false;
            lockRect.gameObject.SetActive(false);

            var ribbonRect = MakeRect("Ribbon", card, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -8), new Vector2(372, 164));
            var ribbon = ribbonRect.gameObject.AddComponent<Image>();
            ribbon.sprite = Ui("UI_Ribbon_Blue");
            ribbon.preserveAspect = true;
            ribbon.raycastTarget = false;
            var title = Label(MakeRect("Title", ribbonRect, new Vector2(0.14f, 0.38f), new Vector2(0.86f, 0.9f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero),
                "Text", "HOUSES", 38, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            title.enableAutoSizing = true;
            title.fontSizeMin = 24;
            title.fontSizeMax = 44;
            title.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            if (outline != null) title.fontSharedMaterial = outline;

            var info = Label(MakeRect("Info", card, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(-20, 80)),
                "Text", "3 buildings", 46, TextAnchor.MiddleCenter, brown, FontStyle.Bold);

            var badge = MakeRect("Badge", card, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-36, -118), new Vector2(80, 80));
            var badgeImg = badge.gameObject.AddComponent<Image>();
            badgeImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.CirclePath);
            badgeImg.color = new Color(0.90f, 0.17f, 0.14f);
            badgeImg.raycastTarget = false;
            var badgeText = Label(badge, "Text", "1", 42, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            if (outline != null) badgeText.fontSharedMaterial = outline;
            badge.gameObject.SetActive(false);

            cardTemplate = card.gameObject.AddComponent<ShopCategoryCardView>();
            Wire(cardTemplate, ("button", button), ("ribbon", ribbon), ("titleText", title), ("picture", picture), ("infoText", info),
                ("badge", badge.gameObject), ("badgeText", badgeText), ("lockOverlay", lockRect.gameObject));
            card.gameObject.SetActive(false);
            return frame.gameObject;
        }

        /// <summary>Category art for the Build cards, in BuildingShopPanel.TabOrder.</summary>
        private static readonly string[] CardArtNames = { "Houses", "Town", "Factories", "Farm", "Decorations" };
        private static readonly string[] RibbonArtNames = { "Blue", "Yellow", "Orange", "Green", "Pink" };

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
