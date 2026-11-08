using System;
using System.IO;
using System.Linq;
using TownBuilder.Domain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TownBuilder.EditorTools
{
    /// <summary>
    /// Renders the Town scene with a sample layout to a PNG, without entering Play mode and without saving the scene.
    /// Used to verify art conventions (pivots, isometric alignment, depth sorting) after importing new assets.
    /// Output: &lt;project&gt;/Temp/TownPreview.png (menu) or the path after -previewOut (command line).
    /// </summary>
    public static class PreviewCapture
    {
        [MenuItem("TownBuilder/Debug/Capture Town Preview", priority = 40)]
        public static void CaptureMenu()
        {
            var path = Capture(Path.Combine(Directory.GetCurrentDirectory(), "Temp", "TownPreview.png"));
            EditorUtility.RevealInFinder(path);
        }

        public static void CaptureFromCommandLine()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                int i = Array.IndexOf(args, "-previewOut");
                string output = i >= 0 && i + 1 < args.Length ? args[i + 1] : Path.Combine(Directory.GetCurrentDirectory(), "Temp", "TownPreview.png");
                Capture(output);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static string Capture(string outputPath, HudState hud = HudState.None)
        {
            var scene = EditorSceneManager.OpenScene(SceneBuilder.TownPath, OpenSceneMode.Single);
            var content = AssetDatabase.LoadAssetAtPath<GameContent>(ProjectSetup.ContentPath);
            var prefab = AssetDatabase.LoadAssetAtPath<BuildingView>(ProjectSetup.BuildingPrefabPath);
            var grid = UnityEngine.Object.FindAnyObjectByType<IsoGrid>();
            var cam = UnityEngine.Object.FindAnyObjectByType<Camera>();
            var highlighter = UnityEngine.Object.FindAnyObjectByType<FootprintHighlighter>();
            grid.Build();

            var layout = new (string id, int x, int y)[]
            {
                ("town_hall", 18, 18), ("cottage", 13, 19), ("cottage", 13, 22), ("barn", 20, 12),
                ("bakery", 17, 12), ("silo", 24, 19), ("cottage", 18, 22), ("feed_mill", 23, 12), ("chicken_coop", 21, 23), ("dairy_shed", 24, 22), ("sugar_mill", 27, 21), ("creamery", 27, 17), ("train_station", 16, 25),
                ("field", 21, 17), ("field", 22, 17), ("field", 23, 17), ("field", 24, 17),
                ("field", 21, 18), ("field", 22, 18), ("field", 23, 18), ("field", 24, 18),
            };

            // Crop stage per field (index into growth sprites; -1 = empty) to check crop art and depth sorting.
            var cropStages = new (string crop, float progress)[]
            {
                ("wheat", 0f), ("wheat", 0.4f), ("wheat", 0.8f), ("wheat", 1f),
                ("corn", 0f), ("corn", 0.4f), ("corn", 0.8f), ("corn", 1f),
            };
            int fieldIndex = 0;

            var town = new Town(grid.Bounds, grid.BlockedCells);
            foreach (var (id, x, y) in layout)
            {
                var def = content.Buildings.First(b => b.Id == id);
                var result = town.TryAdd(Guid.NewGuid().ToString("N"), id, new GridCoord(x, y), def.FootprintSize, out _);
                if (result != PlacementResult.Valid) Debug.LogWarning($"[Preview] {id} at ({x},{y}): {result}");
                var view = UnityEngine.Object.Instantiate(prefab);
                view.Setup(id, def);
                view.SetAnchor(grid.FootprintAnchorWorld(new GridCoord(x, y)));

                if (def.IsFarmPlot && fieldIndex < cropStages.Length && content.TryGetCrop(cropStages[fieldIndex].crop, out var crop))
                {
                    // Same setup as FarmingController.GetCropRenderer.
                    var cropGo = new GameObject("Crop");
                    cropGo.transform.SetParent(view.transform, false);
                    cropGo.transform.localPosition = new Vector3(0f, -FarmingController.CropSortOffset, 0f);
                    var sr = cropGo.AddComponent<SpriteRenderer>();
                    sr.sortingLayerName = SortingLayers.Objects;
                    sr.spriteSortPoint = SpriteSortPoint.Pivot;
                    sr.sprite = crop.SpriteForProgress(cropStages[fieldIndex].progress);
                    fieldIndex++;
                }
            }

            // Show a placement preview (bakery) on free grass.
            var bakery = content.Buildings.First(b => b.Id == "bakery");
            var previewOrigin = new GridCoord(10, 24);
            highlighter.Show(previewOrigin, bakery.FootprintSize, new Color(0.35f, 1f, 0.45f, 0.55f));
            if (hud == HudState.Placing)
            {
                var ghost = UnityEngine.Object.Instantiate(prefab);
                ghost.Setup("preview", bakery);
                ghost.SetAnchor(grid.FootprintAnchorWorld(previewOrigin));
                ghost.SetTint(new Color(1f, 1f, 1f, 0.85f));
            }

            var center = grid.FootprintCenterWorld(new GridCoord(17, 18), new GridCoord(3, 3));
            cam.transform.position = new Vector3(center.x, center.y, -10f);
            cam.orthographicSize = 6.5f;

            if (hud != HudState.None) PrepareTownHud(cam, content, bakery, hud);
            Render(cam, outputPath);

            // Discard the temporary objects: reopen the scene from disk.
            EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
            return outputPath;
        }

        public enum HudState { None, Idle, Shop, Placing, Field, Barn, Factory, Orders, Construction, Effects, Train }

        /// <summary>Command line: renders MainMenu + Town (idle, shop, placing) with UI into the folder after -previewOut.</summary>
        public static void CaptureUiFromCommandLine()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                int i = Array.IndexOf(args, "-previewOut");
                string folder = i >= 0 && i + 1 < args.Length ? args[i + 1] : Path.Combine(Directory.GetCurrentDirectory(), "Temp");
                CaptureMainMenu(Path.Combine(folder, "Preview_MainMenu.png"));
                Capture(Path.Combine(folder, "Preview_Town_Idle.png"), HudState.Idle);
                Capture(Path.Combine(folder, "Preview_Town_Shop.png"), HudState.Shop);
                Capture(Path.Combine(folder, "Preview_Town_Placing.png"), HudState.Placing);
                Capture(Path.Combine(folder, "Preview_Town_Field.png"), HudState.Field);
                Capture(Path.Combine(folder, "Preview_Town_Barn.png"), HudState.Barn);
                Capture(Path.Combine(folder, "Preview_Town_Factory.png"), HudState.Factory);
                Capture(Path.Combine(folder, "Preview_Town_Orders.png"), HudState.Orders);
                Capture(Path.Combine(folder, "Preview_Town_Construction.png"), HudState.Construction);
                Capture(Path.Combine(folder, "Preview_Town_Effects.png"), HudState.Effects);
                Capture(Path.Combine(folder, "Preview_Town_Train.png"), HudState.Train);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static void CaptureMainMenu(string outputPath)
        {
            var scene = EditorSceneManager.OpenScene(SceneBuilder.MainMenuPath, OpenSceneMode.Single);
            var cam = UnityEngine.Object.FindAnyObjectByType<Camera>();
            var canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
            AttachCanvas(canvas, cam);
            Find(canvas, "SettingsPanel").SetActive(false);
            Render(cam, outputPath);
            EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
        }

        private static void PrepareTownHud(Camera cam, GameContent content, BuildingDefinition placing, HudState state)
        {
            var canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
            AttachCanvas(canvas, cam);

            var wallet = new Wallet(1350, 20);
            var progress = new PlayerProgress(1, 0);
            var barn = new Inventory(50); barn.TryAdd("wheat", 12); barn.TryAdd("corn", 6);
            UnityEngine.Object.FindAnyObjectByType<TopBarView>().Bind(wallet, progress, barn);

            var taskRows = canvas.GetComponentsInChildren<TaskRowView>(true);
            taskRows[0].Show("Harvest 6 Wheat", 4, 6, "+20 coins  +3 XP", false);
            taskRows[1].Show("Build a Bakery", 1, 1, "+40 coins  +5 XP", true);
            taskRows[2].Show("Make 2 Bread", 0, 2, "+30 coins  +4 XP", false);

            Find(canvas, "PlacementActionBar").SetActive(state == HudState.Placing);
            Find(canvas, "SelectionActionBar").SetActive(false);
            Find(canvas, "BottomNavigation").SetActive(state == HudState.Idle);

            if (state == HudState.Placing)
            {
                Find(canvas, "PlacementActionBar").transform.Find("Info").GetComponent<TMPro.TMP_Text>().text =
                    $"{placing.DisplayName}  -  {placing.CoinCost:N0} coins\nDrag or tap to position";
            }

            if (state == HudState.Construction)
            {
                var selection = Find(canvas, "SelectionActionBar");
                selection.SetActive(true);
                selection.transform.Find("Name").GetComponent<TMPro.TMP_Text>().text = "Bakery";
                var root = Find(canvas, "ConstructionPanel").transform.Find("Root").gameObject;
                root.SetActive(true);
                root.transform.Find("Title/Text").GetComponent<TMPro.TMP_Text>().text = "Bakery: under construction";
                root.transform.Find("Status/Text").GetComponent<TMPro.TMP_Text>().text = "Ready in 1m 12s";
                root.transform.Find("Buttons/AdButton/Label").GetComponent<TMPro.TMP_Text>().text = "Finish now\n(watch ad)";
                root.transform.Find("Buttons/GemsButton/Label").GetComponent<TMPro.TMP_Text>().text = "Finish now\n2 gems"; // same format as ConstructionPanel

                // Hammer badge above the bakery, like ConstructionController does.
                var icon = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.ConstructionIconPath);
                var bakeryView = UnityEngine.Object.FindObjectsByType<BuildingView>().First(v => v.Definition != null && v.Definition.Id == "bakery");
                var go = new GameObject("ConstructionIcon");
                go.transform.SetParent(bakeryView.transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = icon;
                sr.sortingLayerName = SortingLayers.WorldUI;
                float top = bakeryView.SpriteRenderer.bounds.max.y - bakeryView.transform.position.y;
                go.transform.localPosition = new Vector3(bakeryView.SpriteRenderer.bounds.center.x - bakeryView.transform.position.x, top + 0.3f, 0f);
                go.transform.localScale = Vector3.one * 0.5f;
            }

            if (state == HudState.Train)
            {
                // Station with the train at the platform, its panel with sample crates (two loaded).
                var station = UnityEngine.Object.FindObjectsByType<BuildingView>().First(v => v.Definition != null && v.Definition.Id == "train_station");
                if (content.TrainAtStationSprite != null) station.SpriteRenderer.sprite = content.TrainAtStationSprite;
                var selection = Find(canvas, "SelectionActionBar");
                selection.SetActive(true);
                selection.transform.Find("Name").GetComponent<TMPro.TMP_Text>().text = "Train Station";
                var root = Find(canvas, "TrainPanel").transform.Find("Root").gameObject;
                root.SetActive(true);
                root.transform.Find("Status/Text").GetComponent<TMPro.TMP_Text>().text = "Loaded 2/6";
                root.transform.Find("CallButton").gameObject.SetActive(false);
                root.transform.Find("SendButton/Label").GetComponent<TMPro.TMP_Text>().text = "Send train" + System.Environment.NewLine + "(no bonus)";
                var samples = new (string id, int amount, int have, long coins, long xp, bool loaded)[]
                {
                    ("wheat", 5, 12, 14, 2, true), ("bread", 2, 2, 34, 6, true), ("corn", 4, 1, 23, 4, false),
                    ("egg", 2, 0, 73, 13, false), ("chicken_feed", 3, 3, 68, 12, false), ("sugar", 2, 0, 112, 20, false),
                };
                var views = root.GetComponentsInChildren<TrainCrateView>(true);
                for (int i = 0; i < views.Length; i++)
                {
                    var sample = samples[i];
                    content.TryGetItem(sample.id, out var item);
                    string bonus = i == 2 ? "+1 Planks" : i == 5 ? "+1 Bricks" : null;
                    views[i].Show(item != null ? item.Icon : null, content.ItemName(sample.id), sample.amount, sample.have,
                        sample.coins, sample.xp, sample.loaded, null, bonus);
                }
            }

            if (state == HudState.Effects)
            {
                // Selected cottage with its outline, plus reward labels as they appear right after a harvest / collect.
                var views = UnityEngine.Object.FindObjectsByType<BuildingView>();
                var cottage = views.First(v => v.Definition != null && v.Definition.Id == "cottage");
                var selection = Find(canvas, "SelectionActionBar");
                selection.SetActive(true);
                selection.transform.Find("Name").GetComponent<TMPro.TMP_Text>().text = cottage.Definition.DisplayName;
                UnityEngine.Object.FindAnyObjectByType<SelectionOutline>().Show(cottage);

                var layer = UnityEngine.Object.FindAnyObjectByType<FloatingTextLayer>(FindObjectsInactive.Include);
                var xpColor = new Color(0.6f, 0.88f, 1f);
                content.TryGetCrop("wheat", out var wheat);
                content.TryGetItem("bread", out var bread);
                var field = views.Where(v => v.Definition != null && v.Definition.IsFarmPlot).OrderByDescending(v => v.transform.position.x).First();
                var bakeryView = views.First(v => v.Definition != null && v.Definition.Id == "bakery");
                layer.Spawn(TopOf(field), new[] { new FloatingLine("+2 Wheat", Color.white, wheat.Icon), new FloatingLine("+1 XP", xpColor) });
                layer.Spawn(TopOf(bakeryView), new[] { new FloatingLine("+1 Bread", Color.white, bread.Icon), new FloatingLine("+3 XP", xpColor) });
                layer.Advance(0.35f); // past the pop-in
            }

            if (state == HudState.Orders)
            {
                var root = Find(canvas, "OrdersPanel").transform.Find("Root").gameObject;
                root.SetActive(true);
                barn.TryAdd("bread", 1);
                var cards = root.GetComponentsInChildren<OrderCardView>(true);
                var samples = new[]
                {
                    new Order(new[] { new OrderLine("bread", 2), new OrderLine("wheat", 4) }, 55, 7),
                    new Order(new[] { new OrderLine("wheat", 3) }, 15, 2),
                    new Order(new[] { new OrderLine("bread", 1) }, 24, 3, availableAtUnixMs: 45_000),
                };
                for (int i = 0; i < cards.Length; i++)
                    cards[i].Show(samples[i], 0, i == 1, content, barn);
            }

            if (state == HudState.Factory)
            {
                var selection = Find(canvas, "SelectionActionBar");
                selection.SetActive(true);
                selection.transform.Find("Name").GetComponent<TMPro.TMP_Text>().text = "Bakery";
                var root = Find(canvas, "FactoryPanel").transform.Find("Root").gameObject;
                root.SetActive(true);
                content.TryGetBuilding("bakery", out var bakeryDef);
                var template = root.GetComponentsInChildren<RecipeButtonView>(true).First();
                foreach (var recipe in bakeryDef.Recipes)
                {
                    var b = UnityEngine.Object.Instantiate(template, template.transform.parent);
                    b.gameObject.SetActive(true);
                    b.Bind(recipe, null);
                    b.Refresh(true, recipe.Id == "bake_bread", false);
                }

                var slots = root.GetComponentsInChildren<QueueSlotView>(true);
                var icon0 = bakeryDef.Recipes[0].Output.Icon;
                slots[0].ShowReady(icon0);
                slots[1].ShowActive(icon0, 0.4f, "1m 48s");
                slots[2].ShowWaiting(bakeryDef.Recipes[1].Output.Icon);
                for (int s = 3; s < slots.Length; s++) slots[s].gameObject.SetActive(false);
                root.transform.Find("Queue/Slots/AddSlotButton/Label").GetComponent<TMPro.TMP_Text>().text = "+1 slot" + System.Environment.NewLine + "250";
                root.transform.Find("QueueTitle/Text").GetComponent<TMPro.TMP_Text>().text = "Queue 3/3";
                root.transform.Find("Queue/Actions/CollectButton/Label").GetComponent<TMPro.TMP_Text>().text = "Collect (1)";
                root.transform.Find("Queue/Actions/FinishNowButton/Label").GetComponent<TMPro.TMP_Text>().text = "Finish now (ad)";
            }

            if (state == HudState.Barn)
            {
                barn.TryAdd("planks", 3);
                barn.TryAdd("bricks", 2);
                barn.TryAdd("nails", 4);
                var barnRoot = Find(canvas, "BarnPanel").transform.Find("Root").gameObject;
                barnRoot.SetActive(true);
                barnRoot.transform.Find("Window/Title/Text").GetComponent<TMPro.TMP_Text>().text = $"Barn   {barn.Total}/{barn.Capacity}";
                barnRoot.transform.Find("Window/Empty").gameObject.SetActive(false);
                barnRoot.transform.Find("Window/ExpandButton/Label").GetComponent<TMPro.TMP_Text>().text = "Expand +25\n200 coins";
                var template = barnRoot.GetComponentsInChildren<BarnItemView>(true).First();
                foreach (var pair in barn.Items)
                {
                    content.TryGetItem(pair.Key, out var item);
                    var view = UnityEngine.Object.Instantiate(template, template.transform.parent);
                    view.gameObject.SetActive(true);
                    view.Show(pair.Key, item, pair.Value, null, null);
                }
            }

            if (state == HudState.Field)
            {
                var selection = Find(canvas, "SelectionActionBar");
                selection.SetActive(true);
                selection.transform.Find("Name").GetComponent<TMPro.TMP_Text>().text = "Field";
                var fieldRoot = Find(canvas, "FieldPanel").transform.Find("Root").gameObject;
                fieldRoot.SetActive(true);
                var template = fieldRoot.GetComponentsInChildren<CropButtonView>(true).First();
                foreach (var crop in content.Crops)
                {
                    var b = UnityEngine.Object.Instantiate(template, template.transform.parent);
                    b.gameObject.SetActive(true);
                    b.Bind(crop, null);
                    b.Refresh(true, true);
                }
            }

            if (state == HudState.Shop)
            {
                var shopRoot = Find(canvas, "BuildingShopPanel").transform.Find("Root").gameObject;
                shopRoot.SetActive(true);
                var template = shopRoot.GetComponentsInChildren<ShopItemView>(true).First();
                foreach (var def in content.Buildings)
                {
                    var item = UnityEngine.Object.Instantiate(template, template.transform.parent);
                    item.gameObject.SetActive(true);
                    item.Bind(def, null);
                    item.Refresh(progress.IsUnlocked(def.UnlockLevel), wallet.CanAffordCoins(def.CoinCost), def.MaxCount == 1);
                }
            }
        }

        private static Vector3 TopOf(BuildingView view)
        {
            var b = view.SpriteRenderer.bounds;
            return new Vector3(b.center.x, b.max.y + 0.15f, 0f);
        }

        private static GameObject Find(Canvas canvas, string name) =>
            canvas.GetComponentsInChildren<Transform>(true).First(t => t.name == name).gameObject;

        private static void AttachCanvas(Canvas canvas, Camera cam)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 5f;
            canvas.sortingLayerName = SortingLayers.WorldUI; // in-game the HUD is an overlay canvas, always on top
            canvas.sortingOrder = 100;
        }

        private static void Render(Camera cam, string outputPath)
        {
            const int width = 1920, height = 1080;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            cam.targetTexture = rt;
            cam.aspect = (float)width / height;
            Canvas.ForceUpdateCanvases();
            var floating = UnityEngine.Object.FindAnyObjectByType<FloatingTextLayer>();
            if (floating != null)
            {
                floating.RefreshPositions(); // labels follow world points: re-project for the render size
                Canvas.ForceUpdateCanvases();
            }

            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            cam.targetTexture = null;

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllBytes(outputPath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            Debug.Log($"[Preview] Saved {outputPath}");
        }
    }
}
