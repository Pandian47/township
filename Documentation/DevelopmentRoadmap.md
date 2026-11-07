# Development Roadmap

These are planning windows, not promises. Each milestone ends with an Android build on a real device and a git tag.

## Milestone 1: Foundation (current)

| # | Task | Status |
|---|---|---|
| 1 | Unity URP 2D project | Done (Unity 6000.6.8f1, from the 2D template) |
| 2 | Android player settings (IL2CPP, ARM64, API 26+, Vulkan + GLES3, landscape) | Done in `ProjectSetup`. **Android Build Support module still needs installing** |
| 3 | Folder structure | Done |
| 4 | Sorting layers and isometric sorting axis | Done |
| 5 | Boot scene | Done |
| 6 | MainMenu scene (Continue, New Game, Settings) | Done |
| 7 | Town scene hierarchy | Done |
| 8 | First terrain assets | Placeholders done. **AI art pending** |
| 9 | Isometric tilemap (36×36 starter map) | Done |
| 10 | Camera: drag, inertia, pinch and wheel zoom, bounds | Done. **Verify on device** |
| 11 | Grid coordinates (cell ↔ world ↔ screen) | Done |
| 12 | Building placement: shop, preview, validation, move, remove | Done |
| 13 | Save / load with backup, migration and autosave | Done (EditMode tests) |

**Acceptance (needs your device test):** open the Android app → enter the town → drag → zoom → place a
building → move it → close the app → reopen → same layout.

Automated coverage today: 29 EditMode tests (grid rules, wallet, save store, migration) and 5 PlayMode tests
(Boot→Menu, new town, place + restart persistence, move/remove, Town Hall limit). Touch gestures need a real device:

### Device checklist (Milestone 1)
- [ ] App starts in landscape, no notch overlap (top bar fully visible)
- [ ] New Game → town with Town Hall in the centre, 1,500 coins
- [ ] One-finger drag pans smoothly; a fling keeps sliding briefly; the view stops at the map edge
- [ ] Pinch zooms around the fingers, limited between close-up and whole-map; no jump when lifting one finger
- [ ] Dragging on a panel or button never moves the camera
- [ ] Build → Cottage: preview appears on free grass, green; drag it onto water/road/another building → red, Place disabled
- [ ] Place: coins drop by 150; tap the cottage → Move → drag → Place; Remove → confirm → gone
- [ ] Town Hall shows "Built" in the shop and its Remove button is disabled
- [ ] Press Home, swipe the app away, reopen → Continue → identical town and coins
- [ ] Frame rate stays smooth (target 60 FPS) while panning and zooming

## Milestone 2: Core loop, v0.1 playable prototype (current)

Reordered from the original plan: farming and production make the game playable, so they come before polish.

| Step | Feature | Status |
|---|---|---|
| 1 | Farming: Field building, Wheat/Corn, growth timers, harvest, barn capacity, rewarded "Finish now" | **Done** (53 tests) |
| 2 | Barn screen: list items, sell for coins, paid barn expansion | **Done** |
| 3 | Production: Bakery recipes (Bread, Corn bread), production queue, timers, "Finish now" ad | **Done** |
| 4 | Orders and levels: order board, coin + XP rewards, level curve, unlocks | Next |

**v0.1 definition of done:** plant wheat → harvest → bake bread → deliver it for coins → place a building →
close and reopen with nothing lost.

### Device checklist (step 1: farming)
- [ ] New Game shows 3 fields to the right of the Town Hall
- [ ] Tap a field → crop picker → Wheat → the field shows sprouts and the panel closes
- [ ] Tap the growing field → time left counts down; "Finish now (watch ad)" plays a test ad and completes the crop
- [ ] Tap a ready field → "+2 Wheat +1 XP" toast, the barn counter increases, the field is empty again
- [ ] Plant, close the app for longer than the grow time, reopen → the crop is ready
- [ ] Build a Barn → barn capacity rises by 50

### Device checklist (step 2: barn)
- [ ] The **Barn** button (bottom bar) and the **Barn 0/50** pill (top bar) both open the Barn screen
- [ ] Harvested crops appear as cards: name, "x12 · 2 coins each", Sell 1, Sell all
- [ ] Sell 1 / Sell all add the right coins and update the barn count; the card disappears at 0
- [ ] Expand (+25 for 200 coins, then 400, 600...) raises capacity; the button greys out if you can't afford it
- [ ] Close and reopen the app: the barn contents and expansion are kept

### Device checklist (step 3: bakery)
- [ ] Build a Bakery (450 coins), then tap it: the Bakery panel shows Bread (2 Wheat, 3m) and Corn Bread (2 Corn + 1 Wheat, 6m)
- [ ] Recipes you can't afford show red ingredients and can't be tapped
- [ ] Tap Bread twice: 2 Wheat is taken each time; the queue shows "3m 00s" (with a bar) then "Waiting"
- [ ] After 3 minutes, a bread icon floats above the Bakery; tap the Bakery to collect: "+1 Bread +3 XP"
- [ ] With 3 items queued, recipe cards grey out (queue full)
- [ ] "Finish now (watch ad)" completes the current item, and the next one starts right away
- [ ] Bread appears in the Barn and sells for 12 coins
- [ ] Queue bread, close the app for 10 minutes, reopen: the bread is ready

## Milestone 3: Town building polish
Construction timers (timestamp-based), placement sound/FX, selection outline, a Tile Palette for map editing,
the first AI art set (tiles, buildings, crops).

## Milestone 4: Factories and economy
More factories and recipes (dairy, sugar, feed mill), production upgrades.

## Milestone 5: Orders and progression
Train deliveries, quests, land expansion.

## Milestone 6: Visual polish (months 7–9)
Building and crop animations, vehicles, UI transitions (switch to TextMeshPro), audio, particles.

## Milestone 7: Backend (months 10–12)
Accounts, cloud saves with conflict resolution, server time (`IClock`), server-validated economy, analytics.

## Milestone 8: Expansion (months 13–24+)
Ship, helicopter, more content, friends and co-op, events, achievements, monetisation, Google Play launch.
