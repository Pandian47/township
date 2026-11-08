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
| dairy_shed | Dairy Shed | 2×2 | 6 | 1,200 | Yes |
| sugar_mill | Sugar Mill | 2×2 | 7 | 1,500 | Yes |
| creamery | Creamery | 2×2 | 8 | 2,000 | Yes |

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

Reward per level-up: 50 × new level coins + 1 gem. The save stores TOTAL XP, so the curve can be rebalanced safely.

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

## Rules

* All currency changes go through `Wallet` (domain), never through UI code.
* Every timer stores a UTC start timestamp from `IClock`.
* Once online features exist, the server must validate premium currency and rewards.
