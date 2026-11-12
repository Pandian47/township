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

### Depth sorting (important)

The 2D renderer (`Settings/Renderer2D.asset`) must use **Transparency Sort Mode = Custom Axis** with axis (0, 1, 0), so
sprites lower on screen draw in front. Until v0.24.2 the mode was left at Default by the project template: sorting was
then decided by creation order and only looked right by luck. `ProjectSetup.ConfigureDepthSorting` now enforces it and
`ContentEconomyTests.DepthSorting_IsCustomAxis_AlongY` guards it.

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
`completedTaskIds`. UI: `TasksPanel` + `TaskRowView` (top-left of the HUD). Since v0.35.2 the list is closed by default:
a round Tasks button (clipboard icon, red badge = tasks ready to claim) toggles it, and every other HUD button closes it
(`TownHudController.InitializeTasks` adds `TasksPanel.Close` to their clicks).

## 6g4. Train

`Train` (Domain) is the state machine: Away (until `ArrivesAtUnixMs`) → AtStation with crates → `SendOff` → Away.
`TrainCrateGenerator` builds crates from `OrderService.Candidates()`. `TrainController` (Systems/TrainController) runs it
only while a finished Train Station exists, pays rewards, keeps the station's own picture (the train is drawn
separately by `TrainMover`), and raises Arrived / CrateFilled / Departed / Rewarded (sound + floating coins via the bridges).
`TrainPanel` opens when the station is selected. Saved in `SaveData.train`.

The station is a **landmark** (`BuildingDefinition.movable = false`, `showInShop = false`, a non-movable starter building).
`TownSaveMapper.EnsureFixedBuildings` puts every landmark at its starter spot on load (old saves get it; a player building
in the way moves to the nearest free spot), and `LoadBuildings` relocates rather than drops a building whose cell became
blocked. The railway is the `RailTilemap` (a blocking tilemap painted by `SceneBuilder.PaintRailway`). The station is
closed (padlock badge, "Opens at level N") until the player reaches its unlock level.

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

## 6g6b. Daily reward (Milestone 7 step 1)

`DailyRewardTrack` (Domain) counts claims per whole local day index: `CanClaim(today)`, `Claim(today)` returns the
calendar slot (streak continues when the last claim was yesterday, otherwise restarts), `LocalDay(utcMs, offsetMs)`.
`DailyRewardService` (Scripts/Daily) works out today from `IClock` + the device's time-zone offset, pays the
`GameContent.dailyRewards` entry (materials into the barn, coins instead when it's full) and raises `Claimed`.
`DailyRewardDialog` shows 7 cards, opens on load when a reward is waiting and re-checks every 30 s for a new day.
Saved in `SaveData.daily` (`lastClaimDay`, `streak`).

## 6g6c. Achievements (Milestone 7 step 2)

`AchievementBook` (Domain) keeps one counter per `AchievementKind` (`Add` for event counts, `RaiseTo` for
"highest so far" values like level and buildings owned) and the claimed tiers per achievement id; it raises
`GoalReached` once when a counter crosses the next unclaimed goal. `AchievementDefinition` (ScriptableObject, GameContent →
Achievements) holds the name, kind and tiers (goal + gems). `AchievementService` (Scripts/Achievements) listens to farming,
production, orders, placement, the train, land and level-ups, syncs the "highest so far" counters on load, and pays gems on
`Claim`. `AchievementsPanel` is the Goals screen + the Goals button badge. Saved in `SaveData.achievements`
(counters by kind name, claimed tiers by id).

## 6g6d. Fruit trees (Milestone 7 step 3)

A fruit tree is a building whose `BuildingDefinition.fruit` points at a `CropDefinition` with `growsOnTree = true`.
It reuses farming: the fruit is a crop in `FarmPlots` keyed by the tree's instance id (saved in `cropPlots` like a
field's crop). `FarmingController.EnsureTreeGrowing` starts it when the tree is added (or loaded without a crop), and
`TryHarvest` calls `FarmPlots.TryHarvestAndRegrow`, which keeps the crop and restarts its timer. The fruit's growth
sprites are the whole tree (unripe / ripe fruit), drawn in place of the plain tree sprite, without the crop sway.
`FieldPanel` shows a selected tree's timer (and leaves tree fruit out of the field crop picker).
Placeholder tree art is drawn from `tree.png` by `PlaceholderArtGenerator.CreateFruitTree/CreateFruitStages`.

## 6g7. Ambient life (Milestone 6 step 1)

* **Crop sway:** crop sprites use `Materials/CropSway.mat` (`Shaders/SpriteSway.shader`, Universal2D pass): vertices move
  sideways by `uv.y^1.5 × amplitude × sin(time + world position)`, so the bottom stays put. Relies on each crop sprite
  being its own texture (no atlas). `FarmingController.cropMaterial`.
* **Chimney smoke:** `BuildingDefinition.chimney` = chimney top on the sprite (0–1 from bottom-left; 0,0 = none), set by
  setup from measurements of the AI art (`ProjectSetup.ChimneyPoints`) and editable in the Inspector. `ChimneySmoke`
  (World/ChimneySmoke) scans for factories with an active job twice a second and emits pooled puff sprites on the Effects layer.
* **Floating badges:** `FloatBob.Place(transform, pos)` replaces direct `localPosition` writes for the ready, hammer and padlock badges.
* **Moving train (step 2):** `TrainMover` (World/TrainMover) listens to `TrainController.Arrived/Departed` (subscribed after
  initialisation, so a train already waiting on load doesn't drive in). It moves one sprite (1×5-cell footprint, Objects
  layer, pivot sorting) along the rail column towards +Y: arrive from `enterFromY` to `stopY` (ease-out), depart to
  `exitToY` (ease-in). There is only **one** train picture: after arriving it stays standing at `stopY` beside the
  platform for as long as the train is at the station (also right after loading a save), and the station building
  always keeps its own empty-platform picture (v0.24.2: the combined "station with train" image was removed).
  Visual only; crates can be loaded while it is still rolling in.
* **Street life (step 3):** `StreetLife` (World/StreetLife) builds a graph from the `RoadTilemap` cells and moves pooled
  agents cell to cell (no U-turns except at dead ends). People: `3 + Residential buildings` (max 10), sidewalk offset,
  random short stops, sprite flipped to face the way they walk. Cars (2): right-hand lane; car sprites are drawn along the
  grid X axis and mirrored for the Y axis; front sprite when driving towards the viewer (−X/−Y), back sprite otherwise.
  Objects layer with pivot sorting. No colliders, so taps go straight through.

## 6g8. UI polish (Milestone 6 step 4)

* `PanelTransition`: added by the scene builder to every panel `Root` and the action bars. On `OnEnable` (play mode only)
  it fades a CanvasGroup in and either pops the `Window` child (0.9 → 1.03 → 1) or slides the panel up 60 px. Closing is instant.
* `RewardFlyer` (canvas layer `RewardFlyLayer`, above the panels): watches `Wallet.Changed` / `PlayerProgress.Changed` and
  flies 1–8 pooled icons on a curved path from the last tap (Input System `Pointer`, last 1.2 s) or the screen centre to
  the Coins / Gems / Level pill. Increases only.
* `PressScale`: added by `ButtonSounds` to every button (except top-bar pills): squash to 0.93 while pressed.

## 6g9. Decorations (Milestone 6 step 5)

Decorations are ordinary `BuildingDefinition`s with `category = Decoration` (no recipes, no build time, `maxCount` 0), so
placement, moving, saving and land rules all apply unchanged. `BuildingShopPanel` has one tab per `BuildingCategory`
with something for sale (v0.27.2: Houses, Town, Factories, Farm, Decorations; order in `BuildingShopPanel.TabOrder`),
cloned at runtime from a `ShopTabView` template with the picture of the category's first building.
v0.32.1: both screens are full windows built by `SceneBuilder.BuildShopFrame` (wooden frame, awning, sign, close); the
category window's sign shows the category name, Back returns, and big `ShopItemView` cards (built/max, bonus, picture,
price button or "Level N" / "Built" / "Max") scroll sideways. The category tabs were removed.
v0.27.3: the shop opens on a **category screen** first (`ShopCategoryCardView` cards: coloured ribbon, one of the
category's buildings in a meadow scene from `PlaceholderArtGenerator.CreateBuildScreenArt`, "N buildings" or the
unlock level, and a badge counting what can be built right now). Tapping a card opens that category's list; Back returns.
The layout, names and art are our own; we don't copy another game's store screen (CLAUDE.md rule 10).
v0.27.4 (owner's design): blue-framed window with a tiled awning (`PlaceholderArtGenerator.AwningPath`), wooden BUILD
sign, round close button, ribbon + picture per category (`BuildingShopPanel.cardRibbons/cardPictures`, AI art in
`Art/UI/Build`, imported with `AiArtImporter` Kind.Ui), red round badges. Top-bar pills have icons (level star, coin,
gem, barn).

## 6g6e. Weekend events (Milestone 7 step 4)

`WeekendCalendar` (Domain) turns a local day index into a weekday, the weekend's key (its Saturday) and the rotation
index. `EventProgress` (Domain) holds the key, event id, points and claimed prizes; `Begin` resets it for a new weekend.
`EventDefinition` (ScriptableObject) = name, description, `pointsFrom` (an `AchievementKind`), three `EventMilestone`s
(goal, coins, gems, optional event-only decoration). `WeekendEventService` (Scripts/Events) picks the event from the
calendar (`Tick`, called every second by `EventPanel`), counts harvests / products / orders, pays prizes, keeps the
set of unlocked decorations and filters the shop (`BuildingShopPanel.SetListingFilter`). `EventPanel` = the town button
+ window. DEV "Start weekend event now" runs one from today for two days. Saved in `SaveData.events`.

## 6g6n. Living production buildings (v0.37.0, Feed Mill first)

`WorkingAnimation` (Scripts/Building) on `BuildingDefinition.Working`: points 0-1 on the sprite (like the chimney
point) for the idle sleep letters, a falling stream (chute tip, fall distance, colour), dust puffs, a carrier's walk
(from → to, carrying a sack one way), a worker on the spot, and the rumble strength. `ProjectSetup.WorkingAnimations`
holds the measured values (re-measure if the art changes; setup rewrites them every run).

`FactoryLife` (Scripts/Effects) scans `ProductionController.BuiltFactoryIds()` / `IsWorking(id)` twice a second and
shows each factory idle (Z letters) or working (grain stream, dust, workers, squash-and-stretch from the pivot; skipped
while a `PopAnimator` plays). Particles are pooled sprites on the Effects layer; workers are children of the building
with a `SortingGroup`, so they are drawn in front of their own building but behind buildings in front of it. Everything
is driven by `Step(dt)` so the editor preview can render frames:
`PreviewCapture.CaptureMillLifeFromCommandLine -previewOut Logs` → `Logs/MillLife/frame_*.png`
(`Documentation/Images/FeedMill_Life.gif`). To add another building, measure its points and add it to
`ProjectSetup.WorkingAnimations`. v0.38.0 added Bakery, Sugar Mill, Creamery, Juice Press, Chicken Coop and Dairy Shed;
`-buildings bakery,sugar_mill,...` renders frames for any of them into `Logs/Life_<id>`
(`Documentation/Images/Factories_Life.gif`).

## 6g6m. Roads, paving and landscape tools (v0.36.0)

Domain (`Domain/Town/Landscape.cs`): `Terrain` gained DirtPath, BrickLane, Boardwalk, CheckerTiles, StoneSlabs, Meadow
(appended; saved by name, no migration). `LandscapeTool` (Ground/Roads/Paving), `LandscapePalette` (styles per tool and
their names), `TerrainRules.IsRoad/IsPaving/IsBlocking` (all roads block), `RoadRoute.Path` (grid route with at most one
corner), `RoadJoins.Mask` (which sides touch a road), and `LandscapeEdit` strokes (`BeginStroke/EndStroke`,
`UndoLastStroke`) plus `Check` (dry run for the route preview).

`JoinedRoadTile` (Scripts/Grid) is a `TileBase` with 16 sprites indexed by the neighbour mask; it refreshes its four
neighbours. `RoadTileBuilder` (Editor) draws the 16 kerbed pictures per road style from the flat tile PNG into
`Art/Environment/Tiles/Roads` (ProjectSetup: missing ones; AiArtImporter: styles whose texture was imported). The
starter map's roads use the joined Stone road tile.

`LandscapeController.Begin(tool)`: paving/ground paint freehand (one stroke per drag or tap); roads draw a pending
route (`SetRoute`, `FlipCorner`, `LayRoute`, `DiscardRoute`) shown with the cell highlight sprites (green/red) and a flag
(`PH_RouteFlag`). `Confirm` = lay route or Done; `Back` = discard route or Cancel; `SelectEraser`; `Undo`.
`LandscapePanel` renders the bar from the controller state (`Render(...)`, also used by the editor preview).
Three seeds: `roads_tool`, `paving_tool`, `landscape` (`BuildingDefinition.LandscapeTool`, shop order Roads, Paving,
Landscape).

## 6g6l. Settings, credits, app icon, store screenshots (v0.35.0, launch prep)

`SettingsPanel` (Scripts/UI) is one window used by both scenes, built by `SceneBuilder.BuildSettingsPanel` in the
wooden Build-window frame (`BuildShopFrame` now takes an optional size): music/sound sliders (`AudioPreferences`),
Privacy policy (opens `AdsConfig.PrivacyPolicyUrl`, or explains it isn't set yet), Credits (a second framed window
scrolling `Data/Text/Credits.txt`, `{version}` replaced), Privacy & ads (UMP form, only when
`IAdService.PrivacyOptionsRequired`), Main menu (town only) and the version. The town's round menu button opens it;
Main menu calls `TownSceneController.ReturnToMainMenu` (save, interstitial, load). `MainMenuController` uses the same
panel without the Main menu button.

`AppIconTools` (Editor) draws a placeholder `Art/AppIcon/AppIcon.png` (1024²) or imports `app_icon.png` from the art
Sources folder (centre square, scaled, opaque), and sets it as the default icon. Unity 6.6 has only adaptive icons on
Android (min SDK 26), whose launcher shows the middle two thirds of the layer, so the adaptive background
`AppIcon_Adaptive.png` is the same picture shrunk to 75 % with its edge pixels stretched outwards (about 89 % of the
picture stays visible); the foreground layer is transparent. Called from `ProjectSetup.Run` and `AiArtImporter.Import`.

`PreviewCapture.CaptureStoreFromCommandLine` (-storeOut, default `Documentation/Store`) renders six 1920×1080 store
screenshots without the DEV button and without the green placement footprint.

## 6g6k. Landscape painting (v0.34.0)

`LandscapeEdit` (Domain): one paint session (before/after per cell, rules: editable = own land and not the railway;
water/road never under a building). `LandscapeMap` (Domain): the saved painted cells. `TerrainRules.IsBlocking`.
`LandscapeController` (Scripts/World) is an `IPointerDragHandler` (priority 200, only while painting) and listens to taps;
it sets tiles on the ground / road / water tilemaps, updates `GridOccupancy.SetBlocked`, and suspends placement taps
(`PlacementSystem.TapsSuspended`). Saved cells are painted before `IsoGrid.Build` on load so blocking is right.
`StreetLife.RefreshRoads` re-reads roads after Done. The shop card is a `BuildingDefinition` with `landscapeTool` (never
placed; `PlacementSystem.BeginPlacement` refuses it). `LandscapePanel` is the paint bar. Saved in `SaveData.landscape`.

## 6g6j. Seasons (Milestone 8 step 4)

`SeasonCalendar` (Domain): month → `Season`, `At(utcMs, offsetMs)`, `Next`. `SeasonController` (Scripts/World) checks the
season every 2 s, swaps the grass tile on the ground tilemap (`Tilemap.SwapTile`, other tiles untouched), swaps the
decoration tree's sprite, and runs a small pool of falling sprites that wraps around the camera view. Seasonal decorations
have `BuildingDefinition.seasonal/season`; the shop hides them out of season via `BuildingShopPanel.AddListingFilter`
(filters from events and seasons add up). `SaveData.lastSeason` remembers the announced season. Season grass and trees
are recoloured from the current grass/tree art by `PlaceholderArtGenerator.CreateSeasonArt` (delete them to regenerate
after new art). DEV "Next season (test)".

## 6g6i. Collections (Milestone 8 step 3)

`CollectionBook` (Domain): `CollectionSetSpec`s (id, source `AchievementKind`, drop chance, item ids), item counts,
completions, `Roll(source, random)` (one item of a set fed by that source, at its chance), `TryComplete` (uses one of
each). `CollectionDefinition` (ScriptableObject) holds names, icons and rewards. `CollectionService` rolls on harvest /
factory collect / delivered order, pays on Complete, has a DEV `DevFindItem`. `CollectionsPanel` + `CollectionRowView`
are the window; the chest button sits under the menu button. Saved in `SaveData.collections`.

## 6g6h. Happiness and home coins (Milestone 8 step 2)

`TownHappiness` (Domain): footprint gap distance, a home's happiness from `HappinessSource`s within a radius (capped at
100), coins per hour (base × (1 + happiness/100)). `HomeCoins` (Domain): last-collected time per home, pending coins up to
a cap, `Collect`. `BuildingDefinition.happiness` is set per decoration. `HappinessController` (Scripts/Town) tracks
finished Residential buildings, shows coin bubbles, collects on tap (tap interceptor) and raises `Rewarded` for the
floating "+coins". `HomePanel` is the selected home's panel. Saved in `SaveData.homeCoins`.

## 6g6g. Delivery truck (Milestone 8 step 1)

`DeliveryTruck` (Domain): AtDepot / Away, `TruckLoad`s of several `TruckLine`s, `TryLoad` (all lines or nothing),
`SendOff` (needs one load; the earnings become the `Cargo`), `UpdateReturn` (hands over the cargo and parks with new loads),
`ReturnNow`. `TruckLoadGenerator` picks different items per load from the order candidates. `TruckController`
(Scripts/Truck) runs it at the finished Truck Depot, pays the cargo on return (coins, XP, materials into the barn),
shows the badge, and calls back with ads or gems. `TruckPanel` + `TruckLoadView` are the depot panel. Saved in
`SaveData.truck` (state, return time, loads, cargo). DEV "Unlock everything" now goes to level 12.

## 6g6f. Skipping waits with gems (v0.29.1)

`TimeSkip.GemCost(remainingMs)` (Domain) is the one price rule: 1 gem per started minute, at least 1. It is used by
`ConstructionSites.GemCostToFinish`, `FarmingController.GemCostToFinish/FinishWithGems` (crops and fruit) and
`ProductionController.GemCostToFinish/FinishWithGems` (the active job; later jobs move up). `FieldPanel` and
`FactoryPanel` show the blue gems button next to the ad button.

## 6h0. Game font

UI text uses **Fredoka SemiBold** (`Fonts/Fredoka-SemiBold SDF.asset`, a dynamic TMP font asset made by
`ProjectSetup.EnsureGameFont`; LiberationSans is its fallback). `EditorUtil.AddText` picks it automatically and skips
TMP's synthetic bold for it. Outlined text uses `Materials/UIFont Outline Rounded.mat`. Licence: ThirdPartyLicenses.md. Placeholder art: `PlaceholderArtGenerator.CreateDecoration` (setup seeds with `Deco`).

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

## 6i. Developer mode (test builds only)

`DevTools` (Scripts/Dev) holds testing shortcuts (currency, level up, unlock all, fill barn, finish timers, unlock land,
daily reward again);
`DevPanel` is the purple DEV button + panel in the HUD. `DevTools.Enabled` is true only in the Editor and when compiled
with `TOWNBUILDER_DEVTOOLS`, which `BuildTools.BuildApk` passes (`extraScriptingDefines`). `BuildAab` does not, so the
Google Play bundle has no DEV button. Shortcuts use the normal systems (rewards, events, saving).

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
