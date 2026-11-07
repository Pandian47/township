# Game Design (living document)

**Working title:** TownBuilder. This is an original farming and town-building simulation for Android, played offline first.

## Core loop (target)

Plant crops → harvest → process them in factories → fulfil orders (truck, train) → earn coins and XP →
level up → unlock buildings, crops and land → grow the town.

## Systems and status

| # | System | Status |
|---|---|---|
| A | Isometric world, camera, terrain, roads, water | **Milestone 1: implemented** |
| B | Building shop, placement, move, remove | **Milestone 1: implemented** (no construction timers yet) |
| C | Save / load, new game / continue | **Milestone 1: implemented** |
| D | Farming: plots, planting, growth timers, harvesting | **Milestone 2 step 1: implemented** (Wheat, Corn) |
| E | Inventory, barn capacity | **Implemented:** barn screen, sell for coins, paid expansion (+25 per level), base 50, +50 per Barn, +30 per Silo |
| F | Factories, recipes, production queues | **Bakery implemented** (Bread, Corn Bread, 3-slot queue, tap to collect, Finish now ad). More factories later |
| G | Coins, gems, XP, levels, rewards | **Implemented:** level curve, level-up rewards (50 × level coins + 1 gem), unlocks |
| H | Orders: truck, train, later ship and helicopter | **Order board implemented** (3 slots, skip cooldown). Train/ship later |
| I | Quests, achievements, daily rewards | Milestone 5 / 8 |
| J | Land expansion | Milestone 5 |
| K | Cloud save, accounts, social, events | Milestone 7+ (online) |

## Version 0.1 "playable prototype" definition of done

A player can open the Android game, plant wheat, wait for it to grow, harvest it, use it to make bread,
earn coins, place a building, and close and reopen the game without losing progress.

## Milestone 2, step 1: farming (current)

* **Field** building (1×1, 20 coins, up to 12). New towns start with 3 fields beside the Town Hall.
* Tap an empty field to get the crop picker: **Wheat** (2 min, free seed, yields 2, +1 XP) or **Corn** (5 min, 3 coins, yields 3, +2 XP).
* Crops grow in real time, including while the app is closed. Four visible growth stages.
* Tap a ready field to **harvest** straight into the barn ("+2 Wheat +1 XP" toast). If the barn is full, harvesting is blocked and the crop stays.
* A growing field shows the time left, a progress bar, and **Finish now (watch ad)**, a rewarded ad that completes the crop.
* The top bar shows XP and **Barn used/capacity**. Removing a field with a crop on it warns that the crop will be lost.

## Milestone 1 scope (done)

* Boot → Main menu (Continue / New Game / Settings with music and SFX volume) → Town.
* 36 × 36 starter map with grass, sand, a lake and roads. Water and roads cannot be built on.
* Camera: drag with inertia, pinch and wheel zoom around the focus point, clamped to the map.
* Build shop with 5 buildings (Town Hall pre-placed, Cottage, Barn, Bakery, Silo locked until level 3).
* Placement preview with green and red validity, drag or tap to move it, Place / Cancel. Coins are deducted.
* Tap a building to select it, then Move, Remove (with confirmation) or Close.
* Automatic saving. The town, coins and level are restored after restarting.
