# Development Roadmap

These are planning windows, not promises. Each milestone ends with an Android build on a real device and a git tag.

## Milestone 1: Foundation (done)

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

## Milestone 2: Core loop, v0.1 playable prototype (done)

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

## Milestone 3: Town building polish (done)

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

## Milestone 4: Factories and economy (done)

| Step | Feature | Status |
|---|---|---|
| 1 | **Soybean** crop + **Feed Mill** (Chicken Feed, Cow Feed), scrolling Build shop, economy content tests | **Done** (v0.9.0) |
| 2 | Animal buildings: **Chicken Coop** (Chicken Feed → Eggs), **Dairy Shed** (Cow Feed → Milk) | **Done** (v0.10.0) |
| 3 | **Sugarcane** crop + **Sugar Mill** (Sugar) | **Done** (v0.11.0) |
| 4 | Sweets and dairy: Bakery Cookies + Cake, new **Creamery** (Butter, Cheese); factory recipe lists scroll | **Done** (v0.12.0) |
| 5 | Production upgrades (+1 queue slot for coins, up to 3 per factory) + economy balance pass (EconomyReport, BalanceTests) | **Done** (v0.13.0) |

**Milestone 4 complete** (v0.13.0). Still open from it: AI art for 21 new images (prompts in ArtPrompts.md).

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

### Device checklist (Milestone 4 step 4: sweets and dairy)
- [ ] Tap the Bakery: 4 recipe cards; swipe the cards sideways to see Cake; locked ones show their level
- [ ] Level 8: Creamery (2,000 coins) appears in the shop; Butter = 1 Milk (8 min)
- [ ] Level 9: Cheese = 2 Milk (15 min); Level 10: Cake = 2 Wheat + 2 Egg + 1 Milk + 1 Sugar (20 min)
- [ ] Cookies, Cake, Butter and Cheese show in the Barn and in orders, with their icons
- [ ] Your existing Bakery (built before this update) also has the new recipes

### Device checklist (Milestone 4 step 5: queue upgrades and balance)
- [ ] Tap a finished factory: a blue "+1 slot / 250" card after the queue slots (greyed out if you can't afford it)
- [ ] Tap it → "Add a queue slot to Bakery for 250 coins?" → Add slot: coins drop by 250, "Bakery queue: 4 slots!", the bakery bounces
- [ ] Next costs are 500 and 750; after 3 the card disappears (6 slots)
- [ ] Close and reopen: the extra slots are still there; a new Bakery starts with 3
- [ ] Level-up rewards are now 75 × level coins (level 2 → +150)

## Milestone 5: Orders and progression (done)

Goal: give players something to aim for beyond the order board, and a reason to keep growing the town.
The three systems feed each other: **tasks** guide new players, the **train** pays in building materials,
and **land expansion** spends those materials.

| Step | Feature | Unlock | Status |
|---|---|---|---|
| 1 | **Town tasks**: 3 active goals ("Harvest 10 Wheat", "Bake 3 Bread", "Build a Feed Mill"...), progress from game events, coin/XP/gem rewards, a new one replaces each claimed task | Level 1 | **Done** (v0.14.0) |
| 2 | **Train Station + train**: the train arrives on a timer with 6 crates asking for items; fill crates for coins and XP, a full train adds a bonus; it leaves and returns later | Level 6 | **Done** (v0.15.0) |
| 3 | **Building materials** (Planks, Bricks, Nails): rewards from the train and some tasks, stored in the barn, sellable | with step 2 | **Done** (v0.16.0) |
| 4 | **Land expansion**: the map starts with a buildable area in the middle; surrounding plots are fenced off and bought with coins + materials | Level 3+ | **Done** (v0.17.0) |
| 5 | Balance pass (EconomyReport extended with train income, materials and land; BalanceTests guard rails) | | **Done** (v0.18.0) |

**Milestone 5 complete** (v0.18.0). **v0.19.0** (after your feedback): the Train Station is a fixed landmark by a
fixed railway (every town has it, it can't be moved, removed or bought), it opens at level 5 instead of 6, and the full
AI art set (48 images) is imported. Balance changes: level-up coins 100 × level, land ring 2 at level 9.
Still open: AI art for the new items (27 images in ArtPrompts.md) and the device checklists below.

Design notes:
* **Tasks** are content (ScriptableObject `TaskDefinition`: type, target id, amount, rewards, unlock level), not code.
  Progress listens to events that already exist (Harvested, Collected, Delivered, BuildingPlaced, LeveledUp, Sold).
  Saved: active task ids + progress.
* **Train**: one station (max 1). A visit = arrival time + 6 crates (item, amount, filled?). Crate items are picked from
  what the player can produce at their level (like orders). Timers are UTC timestamps, so the train works while the app is closed.
  The station and train get placeholder art plus prompts for the AI set.
* **Land**: the 36×36 map is split into plots. New towns own the centre; each other plot shows a fence and a "for sale" sign,
  and costs coins + materials (more for plots further out). Tiles stay the same; locked cells just can't be built on.
  Existing saves: any plot that already contains a building starts unlocked, so nobody loses buildings.
* All new numbers live in GameContent / definitions; EconomyReport and BalanceTests are extended in step 5.

### Device checklist (Milestone 5 step 1: town tasks)
- [ ] New game: a "Tasks" panel top-left with Harvest 4 Wheat, Build a Bakery, Make 2 Bread (progress 0/…)
- [ ] Harvesting wheat counts up the wheat task; when done a green **Claim** button appears with a chime and a toast
- [ ] Claim: coins/XP added (the coin pill pops), a new task takes the slot
- [ ] Building a Bakery completes "Build a Bakery" as soon as it's placed
- [ ] After the guide tasks, repeatable tasks appear (e.g. Harvest 10 Wheat, Make 3 Bread, Deliver 3 orders)
- [ ] Level up: new tasks for the new buildings appear (e.g. "Build a Feed Mill" at level 4)
- [ ] Close and reopen: tasks and their progress are the same; claimed guide tasks don't come back
- [ ] The tasks panel never covers the bottom panels (field, factory, shop)

### Device checklist (Milestone 5 step 2: train)
- [ ] (v0.19) The Train Station stands by the railway from the start, with a padlock; it's not in the shop and Move/Remove are greyed out
- [ ] Tap it before level 5: "Opens at level 5"; reaching level 5: "The Train Station is open!", the padlock goes and the first train arrives
- [ ] Tap the station: 6 crates (item, Have x/y in green/red, reward); Load is enabled only when you have enough
- [ ] Loading a crate: items leave the barn, coins + XP, "+coins" floats above the station
- [ ] All 6 loaded: "Send train / +N bonus" pays the bonus; sending early says "(no bonus)"
- [ ] After sending: the platform is empty, the panel shows "Back in 30m 00s" and "Call now (watch ad)"
- [ ] Close the app for 30+ minutes, reopen: the train is back with new crates
- [ ] A partly loaded train is still partly loaded after closing and reopening

### Device checklist (Milestone 5 step 3: building materials)
- [ ] Some train crates show "+1 Planks / Bricks / Nails" on top; loading one adds the material to the barn and it floats up
- [ ] From level 3, tasks like "Deliver 2 orders" (+1 Planks) and "Make 4 Bread" (+1 Nails) appear
- [ ] Level 5: "Load 3 train crates" task (+2 Planks)
- [ ] Planks, Bricks and Nails show in the Barn with their icons and can be sold (10 / 14 / 8 coins)
- [ ] With a full barn, claiming a task that pays materials says the barn is full and keeps the task claimable

### Device checklist (Milestone 5 step 4: land expansion)
- [ ] New game: the middle of the map is bright, the land around it slightly darker, with a small "for sale" sign on each plot next to your land
- [ ] Building on dark land shows red with "Buy this land first"
- [ ] Tap dark land next to your town: "Land for sale" panel with 400 coins + 2 Planks + 1 Nails (green = enough, red = missing)
- [ ] Below level 3 the button says "Level 3"; with everything it says "Buy land" → coins and materials are spent, the plot turns bright, signs move outward
- [ ] Tapping dark land that isn't next to yours says "Buy the land next to it first"
- [ ] "Buy a plot of land" task (level 3) completes when you buy one
- [ ] Close and reopen: bought land stays bought; an old town (from before this update) keeps every building and the land under it

### Device checklist (Milestone 5 step 5: balance)
- [ ] Level-up rewards are 100 × level coins (level 2 → +200)
- [ ] The outer ring of land (map edge) says "Level 9" until level 9
- [ ] Play from a new game to level 6–7 and note: did you ever wait long for coins to afford a new building or land? Too fast or too slow?

## Milestone 6: Visual polish (done)

Owner's phone test of v0.19.1 passed. TextMeshPro and sound were already done in Milestone 3.

| Step | Feature | Status |
|---|---|---|
| 1 | **Ambient life**: crops sway in the wind (shader), smoke from the chimneys of working factories, "ready"/hammer/padlock badges float | **Done** (v0.20.0) |
| 2 | **The train drives**: arrives along the railway, stops at the station, leaves (placeholder train sprite + art prompt) | **Done** (v0.21.0) |
| 3 | Townspeople and vehicles moving along the roads (placeholder sprites + art prompts) | **Done** (v0.22.0) |
| 4 | UI polish: panels fade/slide or pop in, coins/gems/XP fly to the top bar, buttons squash when pressed | **Done** (v0.23.0) |
| 5 | Decorations (Tree, Flower Bed, Bench, Fence, Fountain) in a new Decorations tab of the Build shop | **Done** (v0.24.0) |

**Milestone 6 complete** (v0.24.0). **v0.24.1:** all 17 AI images for the new visuals imported (train, rail, townsfolk,
car, reward icons, decorations); every placeholder in the game is now replaced by AI art.

### Device checklist (Milestone 6 step 1: ambient life)
- [ ] Growing and ripe crops gently sway; the soil and wooden border of the field stay still
- [ ] A factory that is producing (Bakery, Feed Mill, Sugar Mill, Creamery) puffs smoke from its chimney; it stops when the queue is done
- [ ] The smoke starts at the chimney top (not beside it) at every zoom level
- [ ] "Ready" product badges, the construction hammer and the station padlock float up and down slightly
- [ ] Frame rate stays smooth with several factories smoking and a full farm

### Device checklist (Milestone 6 step 2: moving train)
- [ ] When the train arrives (timer, "Call now", or the station opening at level 5) it rolls in from the lower right along
      the railway, slows down and stays standing at the platform, on the rails and in front of the station (v0.24.2: one
      train only; the station never shows a second, painted-in train)
- [ ] "Send train": the same train pulls away slowly, speeds up and leaves over the upper-left edge
- [ ] The train passes in front of the station and behind nothing it shouldn't (check while zoomed in)
- [ ] Opening the app with the train already waiting: it just stands at the platform (no drive-in)
- [ ] (v0.24.2) Overlapping things are drawn in the right order: whatever is lower on screen is in front (buildings,
      trees, train, people, cars)

### Device checklist (Milestone 6 step 3: street life)
- [ ] A few townsfolk walk along the edges of the roads, turn at crossings and sometimes stop for a moment
- [ ] Two cars drive in the right-hand lane, facing the way they drive (front lights towards you, tail lights away)
- [ ] Building a Cottage adds one more walker (after a couple of seconds), up to 10
- [ ] Walkers never block taps on buildings or fields
- [ ] Frame rate stays smooth

### Device checklist (Milestone 6 step 4: UI polish)
- [ ] Barn, Orders, Shop, dialogs: fade in with the window popping slightly; bottom panels and action bars slide up
- [ ] Sell, deliver, load a crate, claim a task: coin icons fly from your finger to the Coins pill; XP stars fly to the level pill; gems to Gems
- [ ] Spending coins (building, buying land) flies nothing
- [ ] Every button squashes a little while you hold it and springs back
- [ ] Opening panels quickly one after another never leaves a panel half-transparent

### Device checklist (Milestone 6 step 5: decorations)
- [ ] Build → two tabs: Buildings (green when active) and Decorations; switching shows only that kind
- [ ] Tree (30 coins, level 1): placed instantly (no hammer), as many as you like, movable and removable
- [ ] Flower Bed (level 2, also a guide task), Bench (3), Fence (4), Fountain 2×2 (6) unlock by level
- [ ] Decorations are kept after closing and reopening the app

## Milestone 7: More content, offline (current)

Owner's decision (v0.24.3): grow the game with content first and stay offline (no new services, packages or costs).
The backend moves to Milestone 9.

| Step | Feature | Status |
|---|---|---|
| 1 | **Daily reward**: a 7-day calendar with growing rewards (coins, materials, gems on day 7); missing a day restarts at day 1 | Done (v0.25.0) |
| 2 | **Achievements**: long-term goals in tiers (harvest, produce, deliver, build, level) paying gems | Done (v0.26.0) |
| 3 | **Orchard**: fruit trees (apples, cherries) that regrow after harvest + a **Juice Press** factory | Done (v0.27.0) |
| 4 | **Weekend event**: a time-limited goal (e.g. "Harvest Festival") with a special reward and decoration | Planned |
| 5 | Balance pass (daily rewards, achievements and event income in the model) | Planned |

Design notes:
* Days are counted in the phone's **local** time (`IClock` UTC + the device's time-zone offset). Offline means a player
  could cheat by changing the phone's clock; acceptable until the backend (server time) exists.
* All rewards and goals are content (GameContent / ScriptableObjects), not code.

### Device checklist (Milestone 7 step 1: daily reward)
- [ ] First start of the new version: the "Daily reward" calendar opens by itself with Day 1 highlighted
- [ ] Claim day 1: +50 coins (toast + sound), the card turns green "(done)", the button says "Come back tomorrow!"
- [ ] Close and reopen the app the same day: the calendar does **not** open again
- [ ] Next day: it opens with Day 2 highlighted; skipping a day starts at Day 1 again
- [ ] DEV → "Daily reward again" lets you claim the next day straight away (walk through all 7 days; day 7 pays 2 gems)
- [ ] Day 3 / 5 / 6 materials appear in the barn

### Device checklist (Milestone 7 step 2: achievements)
- [ ] Bottom bar has a teal **Goals** button; it opens the Goals screen with 7 rows (name, goal, tier, progress, gems)
- [ ] Harvesting, collecting products, delivering orders and loading crates move the matching progress bars
- [ ] Reaching a goal: toast "Achievement ready", a red number badge on the Goals button, the row turns green with Claim
- [ ] Claim pays the gems (sound + toast), the row moves to the next tier; after tier 3 it turns gold "Complete!"
- [ ] DEV → "Unlock everything (level 10)": Rising Star tiers 1 and 2 become claimable
- [ ] Progress and claimed tiers are kept after closing and reopening the app

### Device checklist (v0.27.3: Build screen)
- [ ] Build opens a big "Build" window with 5 cards: Houses (blue), Town (yellow), Factories (orange), Farm (green),
      Decorations (pink), each with a building picture and "N buildings"
- [ ] The red number on a card = how many of its buildings you can build right now (unlocked, enough coins, not at the limit)
- [ ] A category with nothing unlocked is darkened with a lock and "Level N"
- [ ] Tapping a card opens its buildings; "< Back" returns to the cards; X closes from both screens

### Device checklist (v0.27.2: shop categories)
- [ ] Build opens on **Houses**; tabs: Houses, Town (Town Hall, Barn, Silo), Factories (only factories), Farm (Field,
      Chicken Coop, Dairy Shed, Apple Tree, Cherry Tree), Decorations
- [ ] Each tab shows only its own cards and scrolls back to the start when switched

### Device checklist (Milestone 7 step 3: orchard)
- [ ] DEV → "Unlock everything (level 10)", then Build → Farm: Apple Tree, Cherry Tree; Build → Factories: Juice Press
- [ ] An Apple Tree is placed instantly and shows small green apples; tapping it shows "Apple: 29m left"
- [ ] DEV → "Finish all timers": the apples turn red; tapping the tree picks 2 Apples and green apples appear again
- [ ] A field's crop picker does **not** list Apple or Cherry
- [ ] Juice Press: 3 Apples → Apple Juice; 3 Cherries + 1 Sugar → Cherry Juice
- [ ] Trees keep growing while the app is closed; moving a tree keeps its fruit

## Milestone 8: Expansion
Ship, helicopter, friends and co-op, monetisation, Google Play launch.

## Milestone 9: Backend (moved from 7)
Accounts, cloud saves with conflict resolution, server time (`IClock`), server-validated economy, analytics.
Needs the owner's approval for the service (e.g. Firebase or Google Play Games saved games).
