# Architecture

TownBuilder (working title) is an offline-first 2D isometric farming and town-building game for Android.
It is built with Unity 6 (6000.6), C#, URP 2D, the Input System, uGUI and JSON saves.

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
* **Saving:** `SaveData.cropPlots` (plot id, crop id, planted-at) and `SaveData.inventory`. Grow duration comes from the
  current CropDefinition at load time, so rebalancing also applies to crops already planted.

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
| TextMeshPro UI | Legacy uGUI `Text` for now | No TMP essentials import is needed in batch mode. Migrate to TMP during the visual-polish milestone. |
| "Don't use placeholder art" | Generated placeholder sprites (`PH_*`) that follow the final conventions | Lets the code and pipeline be verified before AI art exists. Replace PNGs one by one (see AssetImportGuide.md). |
