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

Removing a building does not refund coins (yet).

### Construction (`BuildingDefinition.buildSeconds`, `buildXp`)

| Building | Build time | XP on completion |
|---|---|---|
| Cottage | 30 s | 5 |
| Barn | 1 min | 8 |
| Silo | 1.5 min | 6 |
| Bakery | 2 min | 10 |
| Feed Mill | 2.5 min | 12 |
| Town Hall, Field | instant | 0 |

Finish early: a rewarded ad, or 1 gem per started minute left.

| field | Field | 1×1 | 1 | 20 | Yes (max 12, crop lost) |

Storage: base barn capacity 50 (`GameContent.baseStorageCapacity`). Barn +50, Silo +30 (`storageCapacityBonus`).
Paid expansion: +25 per level, level N costs N × 200 coins, up to 10 levels (`storageExpansion*` in GameContent).

Sell prices (`ItemDefinition.sellPrice`): Wheat 2 coins, Corn 4, Soybean 6, Bread 12, Corn Bread 22, Chicken Feed 16, Cow Feed 34.

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

Feeds are the ingredients for animal buildings (Milestone 4 step 2). Until then they can be sold or delivered in orders.

**Automatic checks** (`ContentEconomyTests`, run with the EditMode tests): every recipe's product is worth more than
its ingredients, every recipe belongs to exactly one factory, and no recipe unlocks before its factory or any of
its ingredients.

## Levels (`GameContent` → Levels)

| Level-up | XP needed | Total XP | Unlocks |
|---|---|---|---|
| 1 → 2 | 15 | 15 | Corn, Corn Bread |
| 2 → 3 | 30 | 45 | Silo |
| 3 → 4 | 50 | 95 | Feed Mill, Soybean, Chicken Feed |
| 4 → 5 | 80 | 175 | Cow Feed |
| then | 120, 170, 230, 300, 380, 470, then +100 per level | | |

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
