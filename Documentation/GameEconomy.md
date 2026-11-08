# Game Economy (placeholder values)

All values live in ScriptableObjects (`GameContent`, `BuildingDefinition`, and later crop and recipe definitions).
Nothing is hard-coded in C#. The numbers below are **examples to start balancing from**. They are not taken from any existing game.

## New game

| Value | Amount | Where |
|---|---|---|
| Starting coins | 1,500 | GameContent.startingCoins |
| Starting gems | 20 | GameContent.startingGems |
| Starter buildings | Town Hall at (18, 18) | GameContent.starterBuildings |

## Buildings (Milestone 1)

| Id | Name | Footprint | Unlock level | Cost (coins) | Removable |
|---|---|---|---|---|---|
| town_hall | Town Hall | 3×3 | 1 | 0 | No |
| cottage | Cottage | 2×2 | 1 | 150 | Yes |
| barn | Barn | 2×2 | 1 | 300 | Yes |
| bakery | Bakery | 2×2 | 1 | 450 | Yes |
| silo | Silo | 1×1 | 3 | 250 | Yes |
| feed_mill | Feed Mill | 2×2 | 4 | 600 | Yes |
| chicken_coop | Chicken Coop | 2×2 | 5 | 800 | Yes |
| dairy_shed | Dairy Shed | 2×2 | 6 | 1,000 | Yes |
| sugar_mill | Sugar Mill | 2×2 | 7 | 1,000 | Yes |
| creamery | Creamery | 2×2 | 8 | 1,400 | Yes |
| train_station | Train Station | 3×2 | 6 | 600 | Yes (max 1) |

Removing a building does not refund coins (yet).

### Construction (`BuildingDefinition.buildSeconds`, `buildXp`)

| Building | Build time | XP on completion |
|---|---|---|
| Cottage | 30 s | 5 |
| Barn | 1 min | 8 |
| Silo | 1.5 min | 6 |
| Bakery | 2 min | 10 |
| Feed Mill | 2.5 min | 12 |
| Chicken Coop | 3 min | 14 |
| Dairy Shed | 4 min | 18 |
| Sugar Mill | 5 min | 20 |
| Creamery | 6 min | 24 |
| Town Hall, Field | instant | 0 |

Finish early: a rewarded ad, or 1 gem per started minute left.

| field | Field | 1×1 | 1 | 20 | Yes (max 12, crop lost) |

Storage: base barn capacity 50 (`GameContent.baseStorageCapacity`). Barn +50, Silo +30 (`storageCapacityBonus`).
Paid expansion: +25 per level, level N costs N × 200 coins, up to 10 levels (`storageExpansion*` in GameContent).

Sell prices (`ItemDefinition.sellPrice`): Wheat 2 coins, Corn 4, Soybean 6, Bread 12, Corn Bread 22, Chicken Feed 16, Cow Feed 34, Egg 26, Milk 52, Sugarcane 8, Sugar 40, Cookies 62, Butter 72, Cheese 140, Cake 205.

## Bakery (implemented, `Data/Recipes`, 3 queue slots)

| Recipe | Ingredients | Time | XP | Product sells for |
|---|---|---|---|---|
| bake_bread → Bread | 2 Wheat | 3 min | 3 | 12 coins |
| bake_corn_bread → Corn Bread | 2 Corn + 1 Wheat | 6 min | 5 | 22 coins |

Value check: 2 Wheat sells for 4 coins, but as Bread it's worth 12 (+3 XP). Processing should always beat selling raw crops.

## Feed Mill (Milestone 4 step 1, 3 queue slots)

| Recipe | Ingredients (raw value) | Time | XP | Product sells for | Unlock |
|---|---|---|---|---|---|
| make_chicken_feed → Chicken Feed | 2 Wheat + 1 Corn (8) | 4 min | 3 | 16 coins | 4 |
| make_cow_feed → Cow Feed | 2 Corn + 2 Soybean (20) | 7 min | 5 | 34 coins | 5 |

## Animal buildings (Milestone 4 step 2, 3 queue slots each)

Animal buildings are factories: queue a feeding, the animals produce while you're away, tap to collect.

| Building | Recipe | Feed (value) | Time | XP | Product sells for | Unlock |
|---|---|---|---|---|---|---|
| Chicken Coop | collect_eggs → Egg | 1 Chicken Feed (16) | 6 min | 4 | 26 coins | 5 |
| Dairy Shed | collect_milk → Milk | 1 Cow Feed (34) | 10 min | 6 | 52 coins | 6 |

Full chain value: 2 Wheat + 1 Corn (8 coins raw) → Chicken Feed (16) → Egg (26).

## Sugar Mill (Milestone 4 step 3, 3 queue slots)

| Recipe | Ingredients (raw value) | Time | XP | Product sells for | Unlock |
|---|---|---|---|---|---|
| make_sugar → Sugar | 3 Sugarcane (24) | 8 min | 5 | 40 coins | 7 |

## Sweets and dairy (Milestone 4 step 4)

| Factory | Recipe | Ingredients (raw value) | Time | XP | Sells for | Unlock |
|---|---|---|---|---|---|---|
| Bakery | bake_cookies → Cookies | 2 Wheat + 1 Sugar (44) | 10 min | 7 | 62 | 8 |
| Bakery | bake_cake → Cake | 2 Wheat + 2 Egg + 1 Milk + 1 Sugar (148) | 20 min | 14 | 205 | 10 |
| Creamery | churn_butter → Butter | 1 Milk (52) | 8 min | 6 | 72 | 8 |
| Creamery | make_cheese → Cheese | 2 Milk (104) | 15 min | 10 | 140 | 9 |

Cake is the first product that needs three production chains (eggs, milk, sugar), so it pays the most.
Setup adds new seed recipes to existing factories (the Bakery got cookies and cake) without touching recipes
already there or their slot count.

**Automatic checks** (`ContentEconomyTests`, run with the EditMode tests): every recipe's product is worth more than
its ingredients, every recipe belongs to exactly one factory, and no recipe unlocks before its factory or any of
its ingredients.

## Levels (`GameContent` → Levels)

| Level-up | XP needed | Total XP | Unlocks |
|---|---|---|---|
| 1 → 2 | 15 | 15 | Corn, Corn Bread |
| 2 → 3 | 30 | 45 | Silo |
| 3 → 4 | 50 | 95 | Feed Mill, Soybean, Chicken Feed |
| 4 → 5 | 80 | 175 | Cow Feed, Chicken Coop, Egg |
| 5 → 6 | 120 | 295 | Dairy Shed, Milk |
| 6 → 7 | 170 | 465 | Sugarcane, Sugar Mill, Sugar |
| 7 → 8 | 230 | 695 | Creamery, Butter, Cookies |
| 8 → 9 | 300 | 995 | Cheese |
| 9 → 10 | 380 | 1,375 | Cake |
| then | 470, then +100 per level | | |

Reward per level-up: 100 × new level coins + 1 gem (`levelUpCoinsPerLevel`: 50 → 75 in v0.13, → 100 in v0.18). The save stores TOTAL XP, so the curve can be rebalanced safely.

## Orders (`OrderGenerator`)

* 3 slots. Items come from unlocked crops and products of unlocked recipes.
* 1 line at level 1, up to 3 lines from level 4. Raw crops: 1 to (2 + level/2) each. Products: 1 to 2 each.
* Reward = sell value × 1.6 + 5 coins, and XP = sell value / 4 (at least 1). Orders always pay more than the barn.
* Skip: the slot waits 60 s, then a new order appears.

## Crops (implemented, `Data/Crops`)

| Id | Growth | Yield | Seed cost | XP | Unlock |
|---|---|---|---|---|---|
| wheat | 2 min | 2 | free | 1 | 1 |
| corn | 5 min | 3 | 3 coins | 2 | 2 |
| soybean | 8 min | 3 | 4 coins | 2 | 4 |
| sugarcane | 12 min | 3 | 5 coins | 3 | 7 |

## Planned (examples)

| Crop | Growth | Yield | Seed cost |
|---|---|---|---|
| Carrot | 10 min | 4 | 4 |

| Bakery product | Ingredients | Time |
|---|---|---|
| Bread | 2 Wheat | 5 min |
| Cookie | 2 Wheat + 1 Sugar | 10 min |
| Cake | 2 Wheat + 2 Sugar + 1 Egg | 20 min |

## Train (Milestone 5 step 2, `GameContent` → Train)

* The Train Station (level 6, 600 coins, 3 min build, 15 XP) gets its first train as soon as it is finished.
* A train brings **6 crates**. Each asks for an item the player can already make (same pool as orders):
  cheap items (value ≤ 10) 3–6 per crate, mid (≤ 40) 2–3, valuable 1–2.
* Crate reward: item value × amount × **1.4** coins and value × amount / **4** XP (selling to the barn = 1×).
* The train waits at the station with no time limit. **Send train**: a completely loaded train pays a bonus of
  **50 %** of all crate coins; a partly loaded one leaves without bonus. It returns after **30 minutes** with new crates
  (also while the app is closed). "Call now" with a rewarded ad skips the wait.

## Land expansion (Milestone 5 step 4, `GameContent` → Land expansion)

* The 36×36 map is split into **6×6-cell plots** (6×6 plots). A new town owns the **2×2 plots in the middle** (cells 12–23).
* A plot can be bought when it shares an edge with land you own. Price by ring (distance from the start area):

| Ring | Plots | Unlock level | Price |
|---|---|---|---|
| 1 (touching the start area) | 12 | 3 | 400 coins + 2 Planks + 1 Nails |
| 2 (map edge) | 20 | 9 | 1,200 coins + 3 Planks + 2 Bricks + 2 Nails |

* Buildings can only be placed or moved onto owned land. Saves from before land existed keep the start area plus every
  plot that has a building on it.
* Guide task "Buy a plot of land" (level 3): 50 coins, 6 XP, 1 Planks.

## Building materials (Milestone 5 step 3, `Data/Materials`)

| Material | Sells for | Where from |
|---|---|---|
| Planks | 10 | train crates, tasks (Deliver 2 orders, Load 3 crates) |
| Bricks | 14 | train crates, tasks (Harvest 12 Corn, Load 4 crates) |
| Nails | 8 | train crates, task (Make 4 Bread) |

* Each train crate has a **35 %** chance (`trainMaterialChance`) to also pay 1 random material.
* Materials are never requested by orders or the train and no factory makes them. They use barn space.
* Selling them is possible but cheap: they're meant for land expansion (step 4).
* Material tasks start at level 3 (orders, bread) and 4 (corn), so materials exist before the train (level 6).

## Town tasks (Milestone 5 step 1, `Data/Tasks`, 3 on screen)

One-off **guide** tasks appear first, in GameContent order, as soon as their level is reached; after that a random
unlocked **repeatable** task fills the slot. A one-off "Build X" task is skipped if X is already built.

| Task | Goal | Level | Reward |
|---|---|---|---|
| guide_harvest_wheat | Harvest 4 Wheat | 1 | 20 coins, 3 XP |
| guide_build_bakery | Build a Bakery | 1 | 40 coins, 5 XP |
| guide_bake_bread | Make 2 Bread | 1 | 30 coins, 4 XP |
| guide_deliver_orders | Deliver 2 orders | 1 | 40 coins, 5 XP |
| guide_build_barn | Build a Barn | 1 | 40 coins, 5 XP |
| guide_harvest_corn | Harvest 6 Corn | 2 | 30 coins, 4 XP |
| guide_build_silo | Build a Silo | 3 | 40 coins, 5 XP |
| guide_build_feed_mill / chicken_coop | Build it | 4 / 5 | 60 coins, 8 XP, 1 gem |
| guide_collect_eggs | Make 3 Egg | 5 | 60 coins, 6 XP |
| guide_build_dairy_shed / sugar_mill | Build it | 6 / 7 | 80 coins, 10 XP, 1 gem |
| guide_build_creamery | Build a Creamery | 8 | 100 coins, 12 XP, 1 gem |
| guide_bake_cake | Make 1 Cake | 10 | 150 coins, 15 XP, 2 gems |
| Repeatable (12) | Harvest 10 Wheat, Make 3 Bread, Deliver 3 orders, Harvest 9 Corn, Make 3 Chicken Feed, Harvest 6 Soybean, Make 4 Egg, Make 3 Milk, Make 2 Sugar, Make 2 Cookies, Make 2 Butter, Make 1 Cheese | 1–9 | 25–120 coins, 4–10 XP |

Task rewards are extra income on top of the balance model (it ignores them, like orders), so they only make levels
a little faster and richer. `ContentEconomyTests.Tasks_AreValid_AndAchievableAtTheirLevel` checks every task can be
done at its level.

## Factory queue upgrades (Milestone 4 step 5)

Every factory (including animal buildings) starts with 3 queue slots. In its panel, the blue **+1 slot** card buys an
extra slot for that building: the n-th extra slot costs n × 250 coins (250, 500, 750), up to 3 extra (6 in total).
Values: `GameContent.queueSlotUpgradeBaseCost`, `maxExtraQueueSlots`. Saved per building (`BuildingSaveData.extraQueueSlots`);
removing the building loses its upgrades. Coins only: gems stay reserved for speed-ups.

## Balance model (v0.13 balance pass)

`Assets/_Game/Editor/EconomyReport.cs` estimates, level by level, how long a level takes and whether the player can afford
the new key buildings (factories and storage) when they unlock. **TownBuilder → Debug → Write Economy Report** writes
the tables to `Documentation/EconomyReport.md`.

Assumptions: 3 fields at level 1 plus one per level (max 12), always growing the best-paying crop; one of each
unlocked factory, always making its best recipe; players at 50% of perfect efficiency; orders ignored (they pay ~60% more,
so real play is richer than the model).

What the reports showed, and what changed:

| Problem (before) | Change |
|---|---|
| At levels 6, 7 and 8 the new building cost more than the player had (down to −1,442 coins at level 8) | Level-up coins 50 → 75 per level; Dairy Shed 1,200 → 1,000, Sugar Mill 1,500 → 1,200, Creamery 2,000 → 1,600 |
| v0.15: the Train Station (600) at level 6 made levels 7–8 short again (−61 / −267); train income (now in the model) makes levels faster, so they earn less per level | Sugar Mill 1,200 → 1,000, Creamery 1,600 → 1,400 |
| v0.18: with land in the model (first plot of each ring bought when it unlocks), levels 6–8 went negative (down to −1,432): ring 1's 400 coins at level 3 carry through, and ring 2's 1,200 at level 7 competed with the Sugar Mill | Level-up coins 75 → 100 per level; land ring 2 unlocks at level 9 instead of 7 |

Now every level's new key buildings and first land plots are affordable on arrival (tightest: 130 coins left at level 6
after the Dairy Shed and Train Station), and levels take from ~12 minutes (level 1) to ~1.2 hours (level 10) of active
play. The model includes train income from level 6 (one visit per 30 min travel + 15 min loading) and building materials
from the train (35 % of crates) and repeatable tasks (3 slots, one task per ~12 minutes, picked evenly). The materials for
the first plot of ring 1 are earned during level 3, those for ring 2 during level 9.

`BalanceTests` (EditMode) keep it that way: every new key building and first land plot affordable on arrival, the first
plot's materials earned within one level of the ring unlocking, every land material obtainable at the first ring's
level, every level between 5 minutes and 2 hours, every crop and recipe earns coins and XP, and no recipe earns more
than twice the average per minute.

## Rules

* All currency changes go through `Wallet` (domain), never through UI code.
* Every timer stores a UTC start timestamp from `IClock`.
* Once online features exist, the server must validate premium currency and rewards.
