# Playtest guide (v0.19.0)

One place for testing the game on a real Android phone. The detailed per-feature checklists stay in
DevelopmentRoadmap.md; this guide is the order to play in and what to write down.

## 1. Install

APK: `Builds/Android/TownBuilder-0.19.0-19.apk` (37 MB, test build, test ads only).

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
   "C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe" install -r "D:\01-Projects\05-Township\TownBuilder\Builds\Android\TownBuilder-0.19.0-19.apk"
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
Either keep playing over a few days, or ask for **debug shortcuts** (a hidden panel in test builds only to add coins,
XP, materials and skip time) to test these in minutes:

- Feed Mill, Chicken Coop, Dairy Shed, Sugar Mill, Creamery (levels 4–8)
- Train Station (by the railway from the start, padlock until level 5): crates, "+1 Planks", Send train bonus, Call now (ad), train back after 30 min
- Buying land: materials spent, signs move outward; outer ring says "Level 9"

## 5. What to send back

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
