# Architecture

TownBuilder (working title) is an offline-first 2D isometric farming and town-building game for Android.
It is built with Unity 6 (6000.6), C#, URP 2D, the Input System, uGUI + TextMeshPro and JSON saves.

This document is the source of truth for how the code is organised. Update it whenever a system is added.

## 1. Layers

```
┌──────────────────────────────────────────────────────────────────────┐
│ TownBuilder.Editor   (Assets/_Game/Editor)        Editor-only tools │
│   ProjectSetup, SceneBuilder, PlaceholderArtGenerator, PreviewCapture│
└───────────────▲──────────────────────────────────────────────────────┘
                │ uses
┌───────────────┴──────────────────────────────────────────────────────┐
│ TownBuilder.Runtime  (Assets/_Game/Scripts)       Unity adapters     │
│   MonoBehaviours, ScriptableObjects, views, input, camera, UI, save  │
└───────────────▲──────────────────────────────────────────────────────┘
                │ uses
┌───────────────┴──────────────────────────────────────────────────────┐
│ TownBuilder.Domain   (Assets/_Game/Scripts/Domain) Pure C# rules     │
│   GridCoord, GridOccupancy, Town, Wallet, PlayerProgress, IClock,    │
│   SaveData DTOs, SaveMigrator. noEngineReferences = true.            │
└──────────────────────────────────────────────────────────────────────┘
```

* **Domain** holds every game rule: what can be placed where, what can be afforded, how saves are versioned.
  It cannot reference `UnityEngine` (the asmdef enforces this), so it can be unit tested without scenes.
* **Runtime** connects the domain to Unity. Views (sprites, UI) subscribe to domain events and never decide rules themselves.
* **Editor** turns the project into a reproducible setup: running *TownBuilder → Setup → Run Full Setup*
  recreates folders, settings, art placeholders, content assets, prefabs and scenes.
* **Tests** (`Assets/_Game/Tests/EditMode`) cover the domain and the save system.

## 2. Composition (no manager singletons)

```
Boot scene                     MainMenu scene                 Town scene
GameBootstrapper ──creates──► GameRoot (DontDestroyOnLoad)
                               └─ GameContext
                                   ├─ GameContent  (static definitions)
                                   ├─ JsonSaveStore
                                   ├─ IClock       (SystemClock)
                                   └─ ActiveSave   (SaveData snapshot)
                              MainMenuController ──► New Game / Continue
                                                              TownSceneController (composition root)
                                                               ├─ builds Town, Wallet, PlayerProgress from ActiveSave
                                                               ├─ IsoGrid.Build()  (reads painted tilemaps)
                                                               ├─ TownBuildingController.Initialize(town)
                                                               ├─ PlacementSystem.Initialize(town, wallet, progress)
                                                               ├─ IsoCameraController.SetWorldBounds(...)
                                                               ├─ TownHudController.Initialize(...)
                                                               └─ autosave (debounced) + save on pause/quit/leave
```

`GameRoot.Current` is the only global access point. Everything else receives dependencies through
`Initialize(...)` calls or serialized Inspector references. Every scene calls `GameRoot.GetOrCreate(content)`,
so MainMenu and Town also run when opened directly in the Editor.

## 3. Data: definitions vs player state

| Kind | Where | Example | Changes at runtime? |
|---|---|---|---|
| Static definitions | ScriptableObjects in `Assets/_Game/Data/...` | `BuildingDefinition` (id, sprite, footprint, cost, unlock level) | Never |
| Content catalogue | `Assets/_Game/ScriptableObjects/GameContent.asset` | list of definitions, starting coins/gems, starter buildings | Never |
| Player state (live) | Domain objects | `Town`, `Wallet`, `PlayerProgress` | Yes |
| Player state (saved) | `SaveData` DTOs → `persistentDataPath/saves/town.json` | building ids + positions, currencies | Snapshot |

Rules:
* Saves reference content by **stable string id** (`BuildingDefinition.Id`), never by asset name or path. Don't change an id once it has shipped.
* Saves never contain Unity object references.
* Unknown ids or buildings that no longer fit are skipped with a warning while loading. They don't crash the game.

## 4. Isometric grid conventions

* `Grid` cell layout **Isometric**, cell size **(1, 0.5, 1)**, sprites at **128 PPU**, so one 128×64 px tile = one cell.
* Grid X axis points up-right on screen, Y axis up-left.
* `Grid.CellToWorld(cell)` is the **bottom corner** of the cell diamond.
* A building with footprint (w, h) at origin `o` covers cells `o.x … o.x+w-1`, `o.y … o.y+h-1`.
* **Building sprite pivot = bottom corner of its footprint diamond**: `pivot = (h / (w + h), 0)` normalised.
  The view's transform sits exactly on `IsoGrid.FootprintAnchorWorld(origin)`.
* Depth sorting: everything that can overlap (buildings, crops, trees, characters, vehicles) is on sorting layer
  **Objects** with **Sprite Sort Point = Pivot**. The URP 2D Renderer (`Assets/_Game/Settings/Renderer2D.asset`)
  uses **Transparency Sort Mode = Custom Axis (0, 1, 0)**, so lower pivots draw in front.
* The buildable area is authored by painting tilemaps. A cell is buildable if `GroundTilemap` has a tile and
  none of the blocking tilemaps (`WaterTilemap`, `RoadTilemap`) do.

Sorting layers (back to front): `Background`, `Ground`, `GroundOverlay`, `Default`, `Objects`, `Effects`, `WorldUI`.

## 5. Input

`PointerInput` turns Enhanced Touch (device) and Mouse (Editor) into gestures:

* **Tap**: press and release without passing the drag threshold (≈0.06 in, DPI-aware).
* **Drag**: handlers registered via `IPointerDragHandler` are asked in priority order. `PlacementSystem` (100) takes the
  drag when it starts on the placement preview. Otherwise `IsoCameraController` (0) pans.
* **Pinch**: zoom around the finger centre, plus two-finger pan. Lifting one finger after a pinch does not start a drag.
* **Mouse wheel**: zoom (Editor).
* Any gesture that **starts over UI** (EventSystem raycast) is ignored by the world.

## 6. Placement state machine

```
Idle ──tap building──► Selected ──Move──► Moving ──Place/Cancel──► Selected
  ▲                       │ Close / tap empty ground
  └───────────────────────┘
Idle ──shop item──► PlacingNew ──Place (pays coins)/Cancel──► Idle
```

Validation always goes through `Town.CanPlace(origin, size, ignoreId)`, which returns `Valid`, `OutOfBounds`,
`BlockedTerrain`, `Overlap` and so on. The preview is tinted red or green and the footprint cells are highlighted.
`BuildingDefinition.maxCount` limits how many of a type the player may own (Town Hall = 1, 0 = unlimited).
`removable = false` protects key buildings from removal.

## 6b. Farming and inventory

* **Domain:** `FarmPlots` holds the crop on each field, keyed by the field's building instance id, using UTC timestamps.
  `Inventory` holds item counts with a capacity (the barn). Both are pure C# and covered by EditMode tests.
* **Content:** `CropDefinition` (grow time, yield, seed cost, XP, 4 stage sprites). A building is a field when
  `BuildingDefinition.isFarmPlot` is set. Storage buildings add `storageCapacityBonus`.
* **Runtime:** `FarmingController` applies costs, yields and XP. It recalculates barn capacity when buildings change,
  draws the crop as a child sprite of the field view (0.001 units lower, so it always sorts just in front of its own field),
  and hooks `PlacementSystem.BuildingTapInterceptor` so a tap on a ready field harvests instead of selecting.
* **UI:** `FieldPanel` (crop picker / timer / Finish now via `IAdService`), barn pill in `TopBarView`, `ToastView`.
* **Items and barn:** every storable thing derives from `ItemDefinition` (id, name, icon, sell price); `CropDefinition`
  now, products next. `GameContent.TryGetItem` finds any item. `StorageService` owns the barn: capacity = base +
  storage buildings + paid expansions (`StorageUpgrades`, saved as `player.storageLevel`). Selling goes through
  `Market.TrySell`, which updates inventory and wallet together. `BarnPanel` is the screen.
* **Saving:** `SaveData.cropPlots` (plot id, crop id, planted-at) and `SaveData.inventory`. Grow duration comes from the
  current CropDefinition at load time, so rebalancing also applies to crops already planted.

## 6c. Production (factories)

* **Domain:** `ProductionQueue` (per factory) runs jobs one after another, using UTC start times. A new job starts when
  the previous one ends, or immediately if idle. Finished jobs wait in their slot until collected, so they still count
  toward the slot limit. `FinishActiveNow` completes the current job and pulls later jobs forward. `Factories` maps
  factory instance id → queue.
* **Content:** `ProductDefinition : ItemDefinition`, and `RecipeDefinition` (output, ingredients, duration, XP, unlock).
  A building is a factory when `BuildingDefinition.recipes` is not empty. `productionSlots` sets the queue size.
* **Runtime:** `ProductionController` takes ingredients from the barn when a recipe is queued. It collects finished
  products into the barn (stopping when the barn is full) and grants XP, and shows a floating product icon above
  factories with something ready. `PlacementSystem` supports several tap interceptors: a ready field harvests and a
  ready factory collects.
* **UI:** `FactoryPanel` (recipe cards, queue slots, Collect, Finish now via `IAdService`).
* **Saving:** `SaveData.productionJobs` (factory id, recipe id, start time), in queue order.

## 6d. Levels and orders

* **Domain:** `LevelCurve` (XP per level, cumulative thresholds). `PlayerProgress` (with a curve) levels up
  automatically in `AddXp` and fires `LeveledUp` once per level gained. A loaded level that is behind its XP is
  repaired, and a level ahead is kept. `OrderBoard`, `Order`, `OrderLine` and `OrderGenerator` are pure and seedable.
  `TryDeliver` takes the items and pays coins + XP in one step.
* **Runtime:** `ProgressionService` pays level-up rewards and reports unlocks (`GameContent.UnlocksAtLevel`).
  `OrderService` fills empty slots with orders for unlocked items, and handles delivering and skipping.
* **UI:** `OrdersPanel` + `OrderCardView`, `LevelUpDialog` (queues several level-ups), XP progress in `TopBarView`.
* **Saving:** `SaveData.orders` (slot, item ids, amounts, rewards, available-at). Level and XP were already in `player`.

## 6e. Construction

* **Domain:** `ConstructionSites` maps instance id → `ConstructionSite` (UTC start and duration). A building without
  an entry is complete. `TakeFinished` returns each finished site exactly once, so XP is granted once.
* **Runtime:** `ConstructionController` starts a site on `PlacementSystem.BuildingPlaced` (new purchases only),
  shows the hammer badge, completes sites (XP, toast, `Completed` event) and handles finishing with an ad or gems.
  `StorageService` and `ProductionController` treat buildings under construction as inactive.
* **Saving:** `BuildingSaveData.constructionEndsAtUnixMs` (0 = built). Duration comes from the current definition.
* **Ads threading:** every AdMob/UMP callback goes through `MobileAdsEventExecutor.ExecuteInUpdate`, so game code
  always runs on Unity's main thread.

## 6f. Audio

`GameAudio` (persistent, on GameRoot) plays the music loop and sound effects by `SoundId` from `SoundLibrary`.
`TownAudioBridge` maps Town events to sounds, and `ButtonSounds` adds the tap sound to every button. See Audio.md.

## 6g. Visual feedback (effects)

`TownEffects` (on the `Systems/TownEffects` object) is the one place that turns Town events into effects,
like `TownAudioBridge` does for sound. Gameplay controllers only raise events:

* `Rewarded(WorldReward)` on Farming (harvest), Production (collect) and Construction (finished): building id,
  items gained, XP. `TownEffects` pops the building and shows "+2 Wheat / +1 XP" via `FloatingTextLayer`.
* `PlacementSystem.BuildingPlaced` and `Town.BuildingMoved` → pop. `PlacementSystem.StateChanged` → `SelectionOutline`
  is shown while a building is in the Selected state.

Pieces (all in `Scripts/Effects`):

* `PopAnimator`: short squash-and-stretch (buildings, scaled around the footprint pivot) or uniform pop (UI pills).
  Added to a transform on demand; always returns it to its original scale.
* `SelectionOutline`: 8 offset copies of the building's sprites drawn as a flat colour by
  `Shaders/SpriteSilhouette.shader` (material `Materials/SpriteSilhouette.mat`, created by setup), hidden inside the
  building by a `SpriteMask`, on the **Effects** sorting layer. Works with any final art, with no extra textures.
* `FloatingTextLayer`: full-screen layer at the back of the HUD canvas (panels draw over it). Labels follow their
  world point every frame, rise and fade over 1.5 s, pooled.
* `TopBarView` pops a pill when its value goes up (coins, gems, XP, barn).

Effects are cosmetic only: no state, nothing saved, and gameplay works the same without them.

## 6g2. Factory queue upgrades

`QueueUpgrades` (Domain) prices the next slot; `Factories` keeps the bought extra slots per factory and applies them in
`GetOrCreate(id, baseSlots)` (base from the definition + extras), so a content change to the base slot count still works.
`ProductionController.TryAddSlot` charges the wallet and raises `SlotAdded` (sound + pop via the bridges).
Saved in `BuildingSaveData.extraQueueSlots` (written by `TownSaveMapper.WriteProduction`, capped to the content maximum on load).

## 6g3. Town tasks

`TaskBoard` (Domain) holds the active tasks (`TaskSpec` + progress), records progress (`Record(kind, targetId, amount)`),
claims completed tasks and refills slots (guide tasks in order, then random repeatables; `alreadyDone` skips "Build X"
when X exists). `TaskDefinition` assets are the content (`GameContent.Tasks`, order matters). `TaskService` maps events
to progress: `FarmingController.Rewarded` → Harvest, `ProductionController.Rewarded` → Collect, `OrderService.Delivered` → Deliver,
`PlacementSystem.BuildingPlaced` → Build; it pays rewards on `Claim` and refills on level-up. Saved in `SaveData.tasks` and
`completedTaskIds`. UI: `TasksPanel` + `TaskRowView` (top-left of the HUD).

## 6g4. Train

`Train` (Domain) is the state machine: Away (until `ArrivesAtUnixMs`) → AtStation with crates → `SendOff` → Away.
`TrainCrateGenerator` builds crates from `OrderService.Candidates()`. `TrainController` (Systems/TrainController) runs it
only while a finished Train Station exists, pays rewards, swaps the station sprite to `GameContent.trainAtStationSprite`
while the train is there, and raises Arrived / CrateFilled / Departed / Rewarded (sound + floating coins via the bridges).
`TrainPanel` opens when the station is selected. Saved in `SaveData.train`.

## 6g5. Building materials

`MaterialDefinition : ItemDefinition` (`GameContent.Materials`, found by `TryGetItem`), so the barn, selling and saving
work like any item. `TrainCrate` may carry `MaterialId`/`MaterialAmount` (generator option); `TrainController.Fill` adds it.
`TaskDefinition.rewardMaterial` is paid by `TaskService.Claim`, which refuses to claim into a full barn.
`TaskKind.LoadCrates` counts `TrainController.CrateFilled`.

## 6g6. Land expansion

`LandPlots` (Domain) splits the map into plots, owns the start area, sells plots next to owned land (`IsForSale`, `Buy`)
and answers `IsCellUnlocked`. `Town.SetLandCheck` makes `CanPlace/TryAdd/TryMove` return `PlacementResult.LockedLand`
for unowned cells (set after saved buildings load, so nothing is ever removed). `LandController` paints the
`LockedLandTilemap` overlay (GroundOverlay layer, order −1) and the "for sale" signs, listens to
`PlacementSystem.GroundTapped`, and buys plots (prices: `GameContent.landTiers` per ring). `LandPanel` shows the price.
Saved in `SaveData.land.ownedPlots` ("x_y"); empty = initialise (start area + plots under buildings).

## 6h. UI text (TextMeshPro)

All UI text is TextMeshPro (`TMP_Text` fields, `TextMeshProUGUI` components). Never add legacy `UnityEngine.UI.Text`.

* TMP is part of the UI package (`com.unity.ugui`) that the project already uses, so no extra package was added.
  Its essential resources (settings, SDF shaders, default font) live in `Assets/TextMesh Pro/` and are committed.
* Font: **Liberation Sans SDF** (SIL Open Font License, bundled with TMP), path in `ProjectSetup.UIFontAssetPath`.
  `Materials/UIFont Outline.mat` is an outline + soft shadow preset of it, used by the floating reward labels.
* Scene text is created by `EditorUtil.AddText/Label`. Button labels auto-size down to 60% so long labels fit.
  Bold uses `characterSpacing -4` because TMP's synthetic bold is wide.
* Setup imports the TMP resources on the first run. Batch mode imports packages asynchronously, so a fresh clone
  without `Assets/TextMesh Pro/` must import them with `-importPackage` first (see AssetImportGuide.md, section 6).

## 7. Save system

* `JsonSaveStore` writes `town.json.tmp`, flushes it to disk, then `File.Replace`s it over `town.json`, keeping the old file as `town.json.bak`.
* On load, if `town.json` is unreadable it falls back to `.bak` (`LoadStatus.LoadedFromBackup`).
* `SaveData.schemaVersion` + `SaveMigrator` upgrade old saves step by step. A save from a **newer** version is
  refused (never overwritten).
* `SaveMigrator.Sanitize` repairs negative currencies, missing lists, duplicate or missing instance ids.
* Saves happen 2 s after a player action (debounced), on `OnApplicationPause(true)`, on quit and when returning to the menu.
* Timers (crops, factories, construction) will store **absolute UTC timestamps** from `IClock`, never countdowns,
  so progress continues while the app is closed. When online features arrive, `IClock` gets a server-time implementation.
* Device settings (volume) live in `PlayerPrefs` through `AudioPreferences`, not in the town save.

## 8. Adding a new system (checklist)

1. Rules and state go in **Domain** (pure C#) with EditMode tests.
2. Static content becomes a **ScriptableObject definition** with a stable `id`, registered in `GameContent`.
3. Saved state gets **new fields in SaveData** (adding is backward compatible; renaming needs a schema bump plus a migration step).
4. A **Runtime** controller is initialised by `TownSceneController`, and views subscribe to domain events.
5. UI gets a view script with serialized references. Extend `SceneBuilder` if the scene is generated.
6. Update this document and `DevelopmentRoadmap.md`.

## 9. Differences from the original ChatGPT plan (and why)

| ChatGPT plan | Implemented | Reason |
|---|---|---|
| `GameManager`, `SaveManager`, `InventoryManager`, `EconomyManager` objects in the scene | One `GameRoot`/`GameContext` + per-scene `TownSceneController`. Rules live in pure C# classes | The plan also said "avoid unnecessary singletons". This keeps systems testable and makes dependencies explicit. |
| Single scripts assembly | Three assemblies: Domain (engine-free), Runtime, Editor, plus Tests | The compiler enforces the separation of logic and rendering, compiles faster and allows fast unit tests. |
| "Isometric or Isometric Z as Y" | **Isometric** + Custom Axis (0,1,0) sorting + pivot-sorted Objects layer | Flat map with no height levels. This is the simplest correct depth sorting for buildings. |
| Characters on their own sorting layer | Characters share the **Objects** layer | Characters must sort in depth against buildings. A separate layer would always draw them on top. |
| Sorting order per object | Sort by pivot Y | No manual order numbers to maintain. |
| Manual project setup | `ProjectSetup` editor script (menu + command line) | The setup is reproducible and reviewable, with no forgotten clicks. |
| TextMeshPro UI | **TextMeshPro** since v0.7.0 (Milestone 3 step 4) | Started on legacy `Text` to keep batch setup simple; switched once the core loop worked. See section 6h. |
| "Don't use placeholder art" | Generated placeholder sprites (`PH_*`) that follow the final conventions | Lets the code and pipeline be verified before AI art exists. Replace PNGs one by one (see AssetImportGuide.md). |
