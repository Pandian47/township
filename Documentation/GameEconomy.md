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

Removing a building does not refund coins (yet).

| field | Field | 1×1 | 1 | 20 | Yes (max 12, crop lost) |

Storage: base barn capacity 50 (`GameContent.baseStorageCapacity`). Barn +50, Silo +30 (`storageCapacityBonus`).
Paid expansion: +25 per level, level N costs N × 200 coins, up to 10 levels (`storageExpansion*` in GameContent).

Sell prices (`ItemDefinition.sellPrice`): Wheat 2 coins, Corn 4 coins.

## Crops (implemented, `Data/Crops`)

| Id | Growth | Yield | Seed cost | XP | Unlock |
|---|---|---|---|---|---|
| wheat | 2 min | 2 | free | 1 | 1 |
| corn | 5 min | 3 | 3 coins | 2 | 1 |

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
