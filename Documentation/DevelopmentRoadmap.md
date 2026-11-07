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

## Milestone 2: Town building polish (weeks 5–8)
Construction timers (timestamp-based), building rotation/flip (if wanted), placement sound/FX,
selection outline shader, a Tile Palette for map editing, the first AI art set (tiles + 5 buildings).

## Milestone 3: Farming (weeks 9–12)
Field plots, CropDefinition, planting UI, growth stages from timestamps, harvesting, inventory, barn capacity.

## Milestone 4: Factories and economy (weeks 13–18)
FactoryDefinition and RecipeDefinition, production queues (timestamp-based), product inventory, XP and level curve.

## Milestone 5: Orders and progression (weeks 19–24)
Truck orders, train deliveries, rewards, level unlocks, quests, land expansion.

## Milestone 6: Visual polish (months 7–9)
Building and crop animations, vehicles, UI transitions (switch to TextMeshPro), audio, particles.

## Milestone 7: Backend (months 10–12)
Accounts, cloud saves with conflict resolution, server time (`IClock`), server-validated economy, analytics.

## Milestone 8: Expansion (months 13–24+)
Ship, helicopter, more content, friends and co-op, events, achievements, monetisation, Google Play launch.
