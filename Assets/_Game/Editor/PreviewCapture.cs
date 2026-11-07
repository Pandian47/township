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
                ("bakery", 17, 12), ("silo", 24, 19), ("cottage", 18, 22),
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

        public enum HudState { None, Idle, Shop, Placing, Field, Barn, Factory }

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

            Find(canvas, "PlacementActionBar").SetActive(state == HudState.Placing);
            Find(canvas, "SelectionActionBar").SetActive(false);
            Find(canvas, "BottomNavigation").SetActive(state == HudState.Idle);

            if (state == HudState.Placing)
            {
                Find(canvas, "PlacementActionBar").transform.Find("Info").GetComponent<UnityEngine.UI.Text>().text =
                    $"{placing.DisplayName}  -  {placing.CoinCost:N0} coins\nDrag or tap to position";
            }

            if (state == HudState.Factory)
            {
                var selection = Find(canvas, "SelectionActionBar");
                selection.SetActive(true);
                selection.transform.Find("Name").GetComponent<UnityEngine.UI.Text>().text = "Bakery";
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
                slots[3].gameObject.SetActive(false);
                root.transform.Find("QueueTitle/Text").GetComponent<UnityEngine.UI.Text>().text = "Queue 3/3";
                root.transform.Find("Queue/Actions/CollectButton/Label").GetComponent<UnityEngine.UI.Text>().text = "Collect (1)";
                root.transform.Find("Queue/Actions/FinishNowButton/Label").GetComponent<UnityEngine.UI.Text>().text = "Finish now (ad)";
            }

            if (state == HudState.Barn)
            {
                var barnRoot = Find(canvas, "BarnPanel").transform.Find("Root").gameObject;
                barnRoot.SetActive(true);
                barnRoot.transform.Find("Window/Title/Text").GetComponent<UnityEngine.UI.Text>().text = $"Barn   {barn.Total}/{barn.Capacity}";
                barnRoot.transform.Find("Window/Empty").gameObject.SetActive(false);
                barnRoot.transform.Find("Window/ExpandButton/Label").GetComponent<UnityEngine.UI.Text>().text = "Expand +25\n200 coins";
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
                selection.transform.Find("Name").GetComponent<UnityEngine.UI.Text>().text = "Field";
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
