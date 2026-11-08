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
| 8 | First terrain assets | Done (AI art set v0.1, v0.8.0) |
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
| 4 | Orders and levels: order board, coin + XP rewards, level curve, unlocks | **Done** |

**v0.1 playable prototype: complete** (version 0.3.0, code 3). Next: the first AI art set (prompts in ArtStyleGuide.md),
your device test, then Milestone 3 (polish).

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

### Device checklist (step 4: orders and levels)
- [ ] Top bar shows "Lv 1 · 0/15 XP" and fills up as you harvest, bake and deliver
- [ ] **Orders** button: 3 orders using only Wheat/Bread at level 1, with "have/need" in green or red
- [ ] Deliver is enabled only when you have everything; delivering pays more coins than selling the same items
- [ ] Skip → "New order in 1m 00s" countdown, then a new order appears
- [ ] Reaching 15 XP → "Level 2!" dialog with +100 coins and +1 gem, "New: Corn, Corn Bread"; Corn is plantable
- [ ] Level 3 unlocks the Silo in the shop
- [ ] Close and reopen: level, XP and orders (including skip timers) are kept

## Milestone 3: Town building polish (current)

| Step | Feature | Status |
|---|---|---|
| 1 | Construction timers: build time per building, hammer badge, buildings work only when finished, build XP, finish early with an ad or gems | **Done** (v0.4.0) |
| 2 | Sound effects and music (placement, harvest, collect, level-up, UI taps): original placeholders, see Audio.md | **Done** (v0.5.0) |
| 3 | Visual feedback: selection outline, harvest/collect pop effects, floating "+2 Wheat" in the world | **Done** (v0.6.0) |
| 4 | UI text on TextMeshPro (sharper text on high-DPI phones) | **Done** (v0.7.0) |
| 5 | First AI art set dropped in (see ArtStyleGuide.md asset list) | **Done** (v0.8.0, 20 images via `AiArtImporter`) |

### Device checklist (Milestone 3 step 1: construction)
- [ ] Buy a Cottage: "Building Cottage... ready in 30s" toast and a hammer badge above it
- [ ] Tap it: "Cottage: under construction", countdown and bar, "Finish now (watch ad)" and "Finish now 1 gem"
- [ ] A Bakery under construction can't bake; its recipe panel appears once it's finished
- [ ] A Barn under construction doesn't add storage until it's finished
- [ ] When finished: "Barn is ready! +8 XP"
- [ ] Close the app while building, reopen after the build time: the building is done

### Device checklist (Milestone 3 step 2: sound)
- [ ] Music starts on the main menu and keeps playing (no restart) when entering the town
- [ ] Every button clicks; placing thunks; planting pops; harvesting chirps; collecting chimes; selling/delivering jingles
- [ ] Level-up plays a fanfare; "Barn is full" / missing ingredients buzz
- [ ] Settings → Music / Sound effects sliders change the volume immediately and are remembered
- [ ] Sound stops when the app is in the background (phone home button)

### Device checklist (Milestone 3 step 3: visual feedback)
- [ ] Tap a building: a pulsing yellow outline appears around it; Close or tapping grass removes it
- [ ] Move: the outline disappears while dragging and comes back after Place
- [ ] Place a new building or move one: it does a quick squash-and-bounce
- [ ] Harvest a ready field: the field bounces, "+2 Wheat" (with icon) and "+1 XP" rise above it and fade in about 1.5 s
- [ ] The label stays on the field while you pan or zoom during those 1.5 s
- [ ] Collect bread: the Bakery bounces, "+1 Bread +3 XP" rises; collecting 2 shows "+2 Bread"
- [ ] A building finishing construction bounces and shows "+XP"
- [ ] Coins / Gems / Level / Barn pills in the top bar briefly grow when the value goes up (sell, deliver, ad reward)
- [ ] Frame rate stays smooth while harvesting several fields quickly

### Device checklist (Milestone 3 step 5: AI art)
- [ ] Buildings stand exactly on their footprint (place, move: the green diamond matches the building's base)
- [ ] Tapping a building selects the building you tapped, also where two buildings overlap
- [ ] Crops grow inside the field's wooden border at all 4 stages, for wheat and corn
- [ ] No visible lines or gaps between ground tiles while panning and zooming
- [ ] No pink/magenta edges around buildings, crops or icons (zoom in close)
- [ ] Icons in the barn, orders, bakery and floating labels show the new bread / wheat / corn art

### Device checklist (Milestone 3 step 4: TextMeshPro text)
- [ ] All text (menu, top bar, panels, buttons, toasts, level-up dialog) is crisp, with no blurry edges, also when you zoom the phone screenshot
- [ ] No empty boxes instead of letters (check the "·" in "Lv 1 · 0 XP" and "x12 · 2 coins each")
- [ ] No text sticks out of its button or panel; "Finish now / 2 gems" fits on two lines
- [ ] Floating "+2 Wheat" labels have a dark outline and are readable on grass and on buildings

## Milestone 4: Factories and economy (current)

| Step | Feature | Status |
|---|---|---|
| 1 | **Soybean** crop + **Feed Mill** (Chicken Feed, Cow Feed), scrolling Build shop, economy content tests | **Done** (v0.9.0) |
| 2 | Animal buildings: **Chicken Coop** (Chicken Feed → Eggs), **Dairy Shed** (Cow Feed → Milk) | **Done** (v0.10.0) |
| 3 | **Sugarcane** crop + **Sugar Mill** (Sugar) | **Done** (v0.11.0) |
| 4 | New recipes from eggs, milk and sugar (for example cookies, cake, cheese) | Next |
| 5 | Production upgrades (extra queue slots for coins/gems) + economy balance pass | Planned |

### Device checklist (Milestone 4 step 1: Feed Mill)
- [ ] Build shop: swipe left/right to see all buildings; the Feed Mill card shows "Level 4" until you reach it
- [ ] Reaching level 4: the level-up dialog lists Feed Mill, Soybean and Chicken Feed
- [ ] Field → Soybean (8 min, 4 coins); harvest gives 3 Soybean
- [ ] Build the Feed Mill (600 coins, 2.5 min); tap it: Chicken Feed (2 Wheat + 1 Corn) and Cow Feed (2 Corn + 2 Soybean, level 5)
- [ ] Collect: "+1 Chicken Feed" floats up; feeds appear in the Barn and can be sold
- [ ] Orders can ask for Soybean and feeds after level 4

### Device checklist (Milestone 4 step 2: animals)
- [ ] Level 5: level-up dialog lists Chicken Coop and Egg; the coop appears in the Build shop (800 coins)
- [ ] Tap the coop: one recipe "Egg" needing 1 Chicken Feed (6 min); queue up to 3
- [ ] Close the app for longer than the timers, reopen: eggs are ready, tap to collect "+1 Egg"
- [ ] Level 6: Dairy Shed (1,200 coins) turns Cow Feed into Milk (10 min)
- [ ] Eggs and milk show in the Barn, sell for 26 / 52 coins, and appear in orders

### Device checklist (Milestone 4 step 3: sugar)
- [ ] Level 7: level-up dialog lists Sugarcane, Sugar Mill and Sugar
- [ ] Field → Sugarcane (12 min, 5 coins): tall canes at stage 4, harvest gives 3
- [ ] Sugar Mill (1,500 coins, 5 min build): 3 Sugarcane → 1 Sugar in 8 min
- [ ] Sugar shows in the Barn (40 coins) and in orders

## Milestone 5: Orders and progression
Train deliveries, quests, land expansion.

## Milestone 6: Visual polish (months 7–9)
Building and crop animations, vehicles, UI transitions (switch to TextMeshPro), audio, particles.

## Milestone 7: Backend (months 10–12)
Accounts, cloud saves with conflict resolution, server time (`IClock`), server-validated economy, analytics.

## Milestone 8: Expansion (months 13–24+)
Ship, helicopter, more content, friends and co-op, events, achievements, monetisation, Google Play launch.
