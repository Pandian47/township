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
| D | Farming: plots, planting, growth timers, harvesting | Milestone 3 |
| E | Inventory, barn capacity | Milestone 3–4 |
| F | Factories, recipes, production queues | Milestone 4 |
| G | Coins, gems, XP, levels, rewards | Coins/gems exist; XP and levels in Milestone 4–5 |
| H | Orders: truck, train, later ship and helicopter | Milestone 5 / 8 |
| I | Quests, achievements, daily rewards | Milestone 5 / 8 |
| J | Land expansion | Milestone 5 |
| K | Cloud save, accounts, social, events | Milestone 7+ (online) |

## Version 0.1 "playable prototype" definition of done

A player can open the Android game, plant wheat, wait for it to grow, harvest it, use it to make bread,
earn coins, place a building, and close and reopen the game without losing progress.

## Milestone 1 scope (current)

* Boot → Main menu (Continue / New Game / Settings with music and SFX volume) → Town.
* 36 × 36 starter map with grass, sand, a lake and roads. Water and roads cannot be built on.
* Camera: drag with inertia, pinch and wheel zoom around the focus point, clamped to the map.
* Build shop with 5 buildings (Town Hall pre-placed, Cottage, Barn, Bakery, Silo locked until level 3).
* Placement preview with green and red validity, drag or tap to move it, Place / Cancel. Coins are deducted.
* Tap a building to select it, then Move, Remove (with confirmation) or Close.
* Automatic saving. The town, coins and level are restored after restarting.
