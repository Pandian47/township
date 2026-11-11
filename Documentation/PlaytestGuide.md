# Playtest guide (v0.27.5)

One place for testing the game on a real Android phone. The detailed per-feature checklists stay in
DevelopmentRoadmap.md; this guide is the order to play in and what to write down.

## 1. Install

APK: `Builds/Android/TownBuilder-0.27.5-36.apk` (37 MB, test build, test ads only, developer mode included).

**Option A: copy the file (no cable needed)**
1. Upload the APK to Google Drive (or send it to yourself), open it on the phone.
2. Android asks to allow installing from that app: **Settings → Allow from this source**, go back, tap **Install**.
3. If an older TownBuilder is installed, it updates and **keeps your save**. To start clean: long-press the app icon →
   App info → Storage → Clear storage (or in the game: Menu → New Game).

**Option B: USB cable (I can do it for you)**
1. On the phone: Settings → About phone → tap **Build number** 7 times → back → Developer options → turn on **USB debugging**.
2. Connect the cable, tap **Allow** on the "Allow USB debugging?" popup.
3. Tell me, and I'll run the install with Unity's `adb`, or run it yourself:
   ```
   "C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe" install -r "D:\01-Projects\05-Township\TownBuilder\Builds\Android\TownBuilder-0.27.5-36.apk"
   ```

## 2. Session A: a new town (30–45 minutes, sound on)

Start with **Menu → New Game**. Play normally and follow the Tasks panel; don't try to be efficient.

| Check | Look for |
|---|---|
| First minute | Landscape, no notch overlap, music plays, text sharp, Tasks panel top-left shows 3 tasks |
| Camera | One-finger pan with a fling, pinch zoom, stops at the map edge; dragging on a panel never moves the map |
| Farming | Plant wheat on a field, wait 2 min, tap to harvest: bounce, "+2 Wheat" floats up, sound, task counts up |
| Building | Build → swipe the shop sideways → Bakery: hammer badge, finishes after 2 min, "+10 XP" |
| Bakery | Queue bread, "+1 slot" card in the queue, collect: "+1 Bread" |
| Orders | Deliver an order, coins pill pops |
| Daily reward | The calendar opens at start; claim day 1 (+50 coins); reopening the app the same day doesn't show it again |
| Orchard (level 8–10) | Apple/Cherry Trees regrow fruit after each pick; Juice Press makes juice (use DEV to reach level 10) |
| Goals | The Goals button opens 7 long-term goals; a red badge shows when one can be claimed (gems) |
| Tasks | Claim a finished task (green button), a new one appears |
| Level 2–3 | Level-up dialog lists the unlocks; Corn and Silo become available |
| Land (level 3) | Dark land with signs around the town; tap it: "Land for sale" panel with coins + Planks + Nails |
| Ads | "Watch ad +100 coins" plays a **test** ad and pays; "Finish now (ad)" works |

## 3. Session B: closing the app (needs waiting)

1. Plant a crop, queue 2–3 products, then **swipe the app away**.
2. Come back after 10+ minutes: crops are ready, products are done, coins and barn are the same as before.
3. Phone home button while playing: sound stops; reopen: everything still there.

## 4. Session C: later features (needs level 4–9)

Reaching the train (level 5) or the outer land ring (level 9) takes **several hours** of normal play.
Use **developer mode** to get there in a minute: tap the purple **DEV** button (top-right corner, test APKs only).

| Button | What it does |
|---|---|
| +10,000 coins +100 gems | Money for buildings, land and speed-ups |
| Level up (+1) | Exactly one level, with the normal level-up reward and dialog |
| Unlock everything (level 10) | Levels up to 10: every building, crop, recipe, the train and both land rings unlock (one level-up dialog per level: tap through them) |
| Max barn + 10 of every item | Barn fully expanded, then 10 of each crop, product and material |
| Finish all timers + call train | Crops ready, factories done, construction finished, train back now |
| Unlock all land | Owns every plot for free |
| Daily reward again | Lets you claim the next calendar day right away (to see all 7 days) |

Suggested order: Unlock everything → +coins → Max barn → build the buildings → Finish all timers. Then test:

- Feed Mill, Chicken Coop, Dairy Shed, Sugar Mill, Creamery (levels 4–8)
- Train Station (by the railway from the start, padlock until level 5): crates, "+1 Planks", Send train bonus, Call now (ad), train back after 30 min
- Buying land: materials spent, signs move outward; outer ring says "Level 9"

Developer mode changes your real save. To go back to normal play afterwards: Menu → New Game.

## 5. Session D: Milestone 6 look and feel (10–15 minutes)

Use developer mode (Unlock everything, +coins, Max barn) so everything is available, then watch rather than rush:

| Check | Look for |
|---|---|
| Crops | Growing and ripe crops sway gently; the soil and wooden border stay still |
| Smoke | Queue products in the Bakery, Feed Mill, Sugar Mill, Creamery: smoke rises from the chimney top, stops when done |
| Badges | "Ready" product icons, construction hammer and station padlock float up and down |
| Train | Send the train, then DEV → Finish all timers: it rolls in from the lower right and stays standing at the platform (one train, in front of the station); Send: it pulls away to the upper left |
| Roads | Townsfolk walk on the road edges and pause; two cars drive in their lane, lights facing the right way; a new Cottage adds a walker |
| Panels | Barn / Orders / Shop pop in; bottom panels and bars slide up; nothing stays half-transparent |
| Rewards | Selling, delivering, claiming: coins fly from your finger to the Coins pill (XP stars, gems too); spending flies nothing |
| Buttons | Every button squashes while held |
| Decorations | Build → Decorations tab: Tree, Flower Bed, Bench, Fence, Fountain; instant, unlimited, movable |
| Smoothness | Panning and zooming stay smooth with smoke, walkers, train and a full farm at the same time |

## 6. What to send back

For each problem, one line is enough. A screenshot helps a lot (power + volume down).

```
Where:   (e.g. Train panel / farming / shop)
What:    (what you did and what happened)
Expected:(what you thought would happen)
How bad: blocker / annoying / small
```

And three quick feelings after Session A:
1. **Pace:** too slow / OK / too fast? (Did you wait for coins or timers with nothing to do?)
2. **Clarity:** was anything confusing (what to do next, what a button does)?
3. **Comfort:** anything too small to tap, hidden behind a panel, or hard to read?

## 7. Replacing art later (prompts in ArtPrompts.md)

Put the files in `D:\01-Projects\05-Township\Sources` with exactly these names, then tell me (or use
TownBuilder → Art → Import AI Art Folder). Missing files simply keep their placeholder.

| File | ArtPrompts.md section |
|---|---|
| `train.png` | Milestone 6 step 2 (locomotive at the upper-left end) |
| `rail.png` | Railway |
| `person_1.png` … `person_4.png` | Milestone 6 step 3 |
| `car_front.png`, then `car_back.png` (same chat) | Milestone 6 step 3 |
| `coin.png`, `gem.png`, `xp_star.png` | Milestone 6 step 4 |
| `tree.png`, `flower_bed.png`, `bench.png`, `fence.png`, `fountain.png` | Milestone 6 step 5 |
