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
                ("bakery", 17, 12), ("silo", 24, 19), ("cottage", 18, 22), ("feed_mill", 23, 12), ("chicken_coop", 21, 23), ("dairy_shed", 24, 22), ("sugar_mill", 27, 21), ("creamery", 27, 17), ("train_station", 10, 18), ("tree", 15, 17), ("tree", 15, 19), ("flower_bed", 17, 21), ("bench", 21, 21), ("fountain", 13, 13),
                ("field", 21, 17), ("field", 22, 17), ("field", 23, 17), ("field", 24, 17),
                ("apple_tree", 25, 17), ("cherry_tree", 26, 17), ("apple_tree", 25, 18), ("cherry_tree", 26, 18), ("juice_press", 17, 24),
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
                    var sway = AssetDatabase.LoadAssetAtPath<Material>(ProjectSetup.SwayMaterialPath);
                    if (sway != null) sr.sharedMaterial = sway;
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

            // Land: start area + plots under the sample buildings are owned; the rest is darkened with "for sale" signs.
            if (hud != HudState.None)
            {
                var land = new LandPlots(grid.Bounds, content.LandPlotSize, new PlotCoord(content.LandStartMin.x, content.LandStartMin.y),
                    new PlotCoord(content.LandStartMax.x, content.LandStartMax.y));
                var occupied = town.Buildings.SelectMany(b => Enumerable.Range(0, b.Size.X * b.Size.Y)
                    .Select(i => new GridCoord(b.Origin.X + i % b.Size.X, b.Origin.Y + i / b.Size.X)));
                land.InitializeDefault(occupied);
                UnityEngine.Object.FindAnyObjectByType<LandController>().PreviewPaint(land);
            }

            var center = grid.FootprintCenterWorld(new GridCoord(17, 18), new GridCoord(3, 3));
            cam.transform.position = new Vector3(center.x, center.y, -10f);
            cam.orthographicSize = 6.5f;

            // Working factories: a smoke plume (puffs at different ages) from every chimney.
            if (hud != HudState.None)
            {
                var smoke = UnityEngine.Object.FindAnyObjectByType<ChimneySmoke>();
                foreach (var v in UnityEngine.Object.FindObjectsByType<BuildingView>().Where(v => v.Definition != null && v.Definition.HasChimney))
                    for (int p = 0; p < 4; p++) smoke.Emit(v, 0.12f + p * 0.55f);
            }

            if (hud != HudState.None) PrepareTownHud(cam, content, bakery, hud);
            Render(cam, outputPath);

            // Discard the temporary objects: reopen the scene from disk.
            EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
            return outputPath;
        }

        public enum HudState { None, Idle, Shop, Placing, Field, Barn, Factory, Orders, Construction, Effects, Train, Land, ShopDecorations, Daily, Goals, ShopFarm, ShopCards, Event, Truck, Home, Collections }

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
                Capture(Path.Combine(folder, "Preview_Town_Decorations.png"), HudState.ShopDecorations);
                Capture(Path.Combine(folder, "Preview_Town_ShopFarm.png"), HudState.ShopFarm);
                Capture(Path.Combine(folder, "Preview_Town_ShopCards.png"), HudState.ShopCards);
                Capture(Path.Combine(folder, "Preview_Town_Event.png"), HudState.Event);
                Capture(Path.Combine(folder, "Preview_Town_Truck.png"), HudState.Truck);
                Capture(Path.Combine(folder, "Preview_Town_Home.png"), HudState.Home);
                Capture(Path.Combine(folder, "Preview_Town_Collections.png"), HudState.Collections);
                Capture(Path.Combine(folder, "Preview_Town_Placing.png"), HudState.Placing);
                Capture(Path.Combine(folder, "Preview_Town_Field.png"), HudState.Field);
                Capture(Path.Combine(folder, "Preview_Town_Barn.png"), HudState.Barn);
                Capture(Path.Combine(folder, "Preview_Town_Factory.png"), HudState.Factory);
                Capture(Path.Combine(folder, "Preview_Town_Orders.png"), HudState.Orders);
                Capture(Path.Combine(folder, "Preview_Town_Construction.png"), HudState.Construction);
                Capture(Path.Combine(folder, "Preview_Town_Effects.png"), HudState.Effects);
                Capture(Path.Combine(folder, "Preview_Town_Train.png"), HudState.Train);
                Capture(Path.Combine(folder, "Preview_Town_Land.png"), HudState.Land);
                Capture(Path.Combine(folder, "Preview_Town_Daily.png"), HudState.Daily);
                Capture(Path.Combine(folder, "Preview_Town_Goals.png"), HudState.Goals);
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
            var progress = new PlayerProgress(1, 6, content.CreateLevelCurve());
            var barn = new Inventory(50); barn.TryAdd("wheat", 12); barn.TryAdd("corn", 6);
            UnityEngine.Object.FindAnyObjectByType<TopBarView>().Bind(wallet, progress, barn);

            var taskRows = canvas.GetComponentsInChildren<TaskRowView>(true);
            taskRows[0].Show("Harvest 6 Wheat", 4, 6, "+20 coins  +3 XP", false);
            taskRows[1].Show("Build a Bakery", 1, 1, "+40 coins  +5 XP", true);
            taskRows[2].Show("Make 2 Bread", 0, 2, "+30 coins  +4 XP", false);

            Find(canvas, "PlacementActionBar").SetActive(state == HudState.Placing);
            Find(canvas, "SelectionActionBar").SetActive(false);
            Find(canvas, "BottomNavigation").SetActive(state == HudState.Idle);
            if (state == HudState.Idle) { var m = UnityEngine.Object.FindAnyObjectByType<TrainMover>(); m?.PreviewAt(m.StopY); } // train standing at the platform
            if (state == HudState.Idle) UnityEngine.Object.FindAnyObjectByType<StreetLife>()?.PreviewPopulate(8, 2);

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

            if (state == HudState.Goals)
            {
                // Some progress: one tier ready, one finished, the rest on the way.
                var panel = UnityEngine.Object.FindAnyObjectByType<AchievementsPanel>(FindObjectsInactive.Include);
                var book = new AchievementBook(content.Achievements.Select(a => a.ToSpec()));
                book.Restore(new[]
                {
                    new System.Collections.Generic.KeyValuePair<AchievementKind, long>(AchievementKind.HarvestCrops, 342),
                    new System.Collections.Generic.KeyValuePair<AchievementKind, long>(AchievementKind.MakeProducts, 120),
                    new System.Collections.Generic.KeyValuePair<AchievementKind, long>(AchievementKind.DeliverOrders, 23),
                    new System.Collections.Generic.KeyValuePair<AchievementKind, long>(AchievementKind.LoadCrates, 4),
                    new System.Collections.Generic.KeyValuePair<AchievementKind, long>(AchievementKind.OwnBuildings, 9),
                    new System.Collections.Generic.KeyValuePair<AchievementKind, long>(AchievementKind.BuyLand, 1),
                    new System.Collections.Generic.KeyValuePair<AchievementKind, long>(AchievementKind.ReachLevel, 16),
                }, new[]
                {
                    new System.Collections.Generic.KeyValuePair<string, int>("harvest_crops", 1),
                    new System.Collections.Generic.KeyValuePair<string, int>("make_products", 1),
                    new System.Collections.Generic.KeyValuePair<string, int>("deliver_orders", 1),
                    new System.Collections.Generic.KeyValuePair<string, int>("own_buildings", 1),
                    new System.Collections.Generic.KeyValuePair<string, int>("reach_level", 3),
                });
                Find(canvas, "AchievementsPanel").transform.Find("Root").gameObject.SetActive(true);
                panel.Render(book, content);
            }

            if (state == HudState.Daily)
            {
                // Day 3 waiting: days 1-2 claimed.
                var root = Find(canvas, "DailyRewardDialog").transform.Find("Root").gameObject;
                root.SetActive(true);
                var backs = root.transform.Find("Window/Days").GetComponentsInChildren<UnityEngine.UI.Image>(true).Where(img => img.name.StartsWith("Day")).ToArray();
                var coin = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.CoinIconPath);
                var gem = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.GemIconPath);
                for (int i = 0; i < backs.Length && i < content.DailyRewards.Count; i++)
                {
                    var e = content.DailyRewards[i];
                    var card = backs[i].transform;
                    backs[i].color = i < 2 ? new Color(0.78f, 0.93f, 0.76f) : i == 2 ? new Color(1f, 0.86f, 0.45f) : Color.white;
                    card.Find("Day/Text").GetComponent<TMPro.TMP_Text>().text = i < 2 ? $"Day {i + 1} (done)" : $"Day {i + 1}";
                    var icon = card.Find("Icon").GetComponent<UnityEngine.UI.Image>();
                    icon.sprite = e.material != null && e.materialAmount > 0 ? e.material.Icon : e.gems > 0 ? gem : coin;
                    var lines = new System.Collections.Generic.List<string>();
                    if (e.coins > 0) lines.Add($"{e.coins:N0} coins");
                    if (e.gems > 0) lines.Add($"{e.gems} gems");
                    if (e.material != null && e.materialAmount > 0) lines.Add($"{e.materialAmount} {e.material.DisplayName}");
                    card.Find("Amount/Text").GetComponent<TMPro.TMP_Text>().text = string.Join(System.Environment.NewLine, lines);
                }

                root.transform.Find("Window/ClaimButton/Label").GetComponent<TMPro.TMP_Text>().text = "Claim day 3";
            }

            if (state == HudState.Land)
            {
                var root = Find(canvas, "LandPanel").transform.Find("Root").gameObject;
                root.SetActive(true);
                root.transform.Find("Info/Text").GetComponent<TMPro.TMP_Text>().text = "6 × 6 tiles to build on";
                var tier = content.LandTierForRing(1);
                var texts = root.transform.Find("Costs").GetComponentsInChildren<TMPro.TMP_Text>(true);
                var icons = root.transform.Find("Costs").GetComponentsInChildren<UnityEngine.UI.Image>(true).Where(img => img.name == "Icon").ToArray();
                texts[0].text = $"{tier.coins:N0} coins";
                texts[0].color = new Color(0.2f, 0.55f, 0.25f);
                icons[0].enabled = false;
                int[] have = { 2, 0 };
                for (int i = 1; i < texts.Length; i++)
                {
                    bool used = i - 1 < tier.materials.Count;
                    icons[i].transform.parent.gameObject.SetActive(used);
                    if (!used) continue;
                    var cost = tier.materials[i - 1];
                    int h = i - 1 < have.Length ? have[i - 1] : 0;
                    texts[i].text = $"{cost.material.DisplayName}  {h}/{cost.amount}";
                    texts[i].color = h >= cost.amount ? new Color(0.2f, 0.55f, 0.25f) : new Color(0.8f, 0.25f, 0.2f);
                    icons[i].sprite = cost.material.Icon;
                }

                root.transform.Find("BuyButton").GetComponent<UnityEngine.UI.Button>().interactable = false;
            }

            if (state == HudState.Train)
            {
                // The train standing at the platform, the station panel with sample crates (two loaded).
                var mover = UnityEngine.Object.FindAnyObjectByType<TrainMover>();
                if (mover != null) mover.PreviewAt(mover.StopY);
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
                root.transform.Find("Queue/Actions/FinishNowButton/Label").GetComponent<TMPro.TMP_Text>().text = "Finish now" + System.Environment.NewLine + "(watch ad)";
                root.transform.Find("Queue/Actions/GemsButton/Label").GetComponent<TMPro.TMP_Text>().text = "Finish now" + System.Environment.NewLine + "2 gems";
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
                foreach (var crop in content.Crops.Where(c => !c.GrowsOnTree))
                {
                    var b = UnityEngine.Object.Instantiate(template, template.transform.parent);
                    b.gameObject.SetActive(true);
                    b.Bind(crop, null);
                    b.Refresh(true, true);
                }
            }

            if (state == HudState.Collections && content.Collections.Count > 0)
            {
                // Meadow set complete (ready), Workshop 3/5 with a spare, Market 1/5.
                var book = new CollectionBook(content.Collections.Select(c => c.ToSpec()));
                foreach (var item in content.Collections[0].Items) book.Add(item.id);
                book.Add(content.Collections[0].Items[0].id);
                if (content.Collections.Count > 1) for (int i = 0; i < 3; i++) book.Add(content.Collections[1].Items[i].id, i == 1 ? 2 : 1);
                if (content.Collections.Count > 2) book.Add(content.Collections[2].Items[3].id);
                Find(canvas, "CollectionsPanel").transform.Find("Root").gameObject.SetActive(true);
                Find(canvas, "CollectionsPanel").GetComponent<CollectionsPanel>().Render(book, content);
            }

            if (state == HudState.Home)
            {
                var selection = Find(canvas, "SelectionActionBar");
                selection.SetActive(true);
                selection.transform.Find("Name").GetComponent<TMPro.TMP_Text>().text = "Cottage";
                Find(canvas, "HomePanel").transform.Find("Root").gameObject.SetActive(true);
                Find(canvas, "HomePanel").GetComponent<HomePanel>().Render("Cottage", 45, TownHappiness.CoinsPerHour(content.HomeCoinsPerHour, 45),
                    content.HomeCoinsMaxHours, content.HappinessRadius, 11);
            }

            if (state == HudState.Truck)
            {
                // Truck parked with three mixed loads: the first already loaded, the second ready, the third short.
                var selection = Find(canvas, "SelectionActionBar");
                selection.SetActive(true);
                selection.transform.Find("Name").GetComponent<TMPro.TMP_Text>().text = "Truck Depot";
                var root = Find(canvas, "TruckPanel").transform.Find("Root").gameObject;
                root.SetActive(true);
                root.transform.Find("Status/Text").GetComponent<TMPro.TMP_Text>().text = "Loaded 1/3. Earnings are paid when the truck comes back.";
                Sprite Icon(string id) => content.TryGetItem(id, out var item) ? item.Icon : null;
                var views = root.GetComponentsInChildren<TruckLoadView>(true);
                string nl = System.Environment.NewLine;
                views[0].Show(new[] { (Icon("bread"), "Bread  6/6", true), (Icon("egg"), "Egg  4/4", true) }, "+187 coins  +16 XP" + nl + "+1 Planks", true, true, null);
                views[1].Show(new[] { (Icon("corn"), "Corn  8/8", true), (Icon("milk"), "Milk  3/3", true) }, "+301 coins  +27 XP" + nl + "+1 Bricks", false, true, null);
                views[2].Show(new[] { (Icon("cookies"), "Cookies  1/3", false), (Icon("wheat"), "Wheat  7/7", true) }, "+320 coins  +28 XP" + nl + "+1 Nails", false, false, null);
                root.transform.Find("SendButton/Label").GetComponent<TMPro.TMP_Text>().text = "Send truck";
                root.transform.Find("CallButton").gameObject.SetActive(false);
                root.transform.Find("GemsButton").gameObject.SetActive(false);
            }

            if (state == HudState.Event && content.WeekendEvents.Count > 0)
            {
                // Harvest Festival, Saturday afternoon: prize 1 claimed, prize 2 ready, prize 3 on the way.
                var ev = content.WeekendEvents[0];
                var p = new EventProgress();
                p.Begin(20771, ev.Id, ev.Goals);
                p.AddPoints(95);
                p.TryClaim(0);
                Find(canvas, "EventPanel").GetComponent<EventPanel>().PreviewRender(ev, p, "1d 9h");
            }

            if (state == HudState.Idle && content.WeekendEvents.Count > 0)
            {
                var ev = content.WeekendEvents[0];
                var p = new EventProgress();
                p.Begin(20771, ev.Id, ev.Goals);
                p.AddPoints(35);
                var panel = Find(canvas, "EventPanel").GetComponent<EventPanel>();
                panel.PreviewRender(ev, p, "1d 9h");
                Find(canvas, "EventPanel").transform.Find("Root").gameObject.SetActive(false);
            }

            if (state == HudState.ShopCards)
            {
                var shopRoot = Find(canvas, "BuildingShopPanel").transform.Find("Root").gameObject;
                shopRoot.SetActive(true);
                shopRoot.transform.Find("Window").gameObject.SetActive(false);
                shopRoot.transform.Find("CardsWindow").gameObject.SetActive(true);
                var shopPanel = Find(canvas, "BuildingShopPanel").GetComponent<BuildingShopPanel>();
                shopPanel.BuildCards(content);
                // Level 6 with the starting coins: some categories have things to build, the orchard is still locked.
                shopPanel.RefreshCards(content, 6, d => d.UnlockLevel <= 6 && d.CoinCost <= wallet.Coins && d.MaxCount != 1);
            }

            if (state == HudState.Shop || state == HudState.ShopDecorations || state == HudState.ShopFarm)
            {
                var category = state == HudState.ShopDecorations ? BuildingCategory.Decoration
                    : state == HudState.ShopFarm ? BuildingCategory.Farming : BuildingCategory.Production;
                var shopRoot = Find(canvas, "BuildingShopPanel").transform.Find("Root").gameObject;
                shopRoot.SetActive(true);
                shopRoot.transform.Find("CardsWindow").gameObject.SetActive(false);
                var shopPanel = Find(canvas, "BuildingShopPanel").GetComponent<BuildingShopPanel>();
                shopRoot.transform.Find("Window").gameObject.SetActive(true);
                shopPanel.SetTitle(category);
                var template = shopRoot.GetComponentsInChildren<ShopItemView>(true).First();
                foreach (var def in content.Buildings.Where(d => d.ShowInShop && !d.EventOnly && d.Category == category))
                {
                    var item = UnityEngine.Object.Instantiate(template, template.transform.parent);
                    item.gameObject.SetActive(true);
                    item.Bind(def, null);
                    bool landmarkOrOne = def.MaxCount == 1 && def.Category != BuildingCategory.Production;
                    item.Refresh(progress.IsUnlocked(def.UnlockLevel), wallet.CanAffordCoins(def.CoinCost), landmarkOrOne, landmarkOrOne ? 1 : 0);
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
