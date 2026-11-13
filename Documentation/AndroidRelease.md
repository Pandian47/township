# Android builds and Google Play release

| File | Use | Signed with |
|---|---|---|
| `Builds/Android/TownBuilder-<version>-<code>.apk` | Install directly on your phone for testing | Unity debug key (automatic) |
| `Builds/Android/TownBuilder-<version>-<code>.aab` | Upload to Google Play Console | **Your upload key** |

`Builds/` is git-ignored. Never commit build outputs, keystores or passwords.

## Test APK

* Unity menu: **TownBuilder → Build → Android APK (test on device)**, or
* command line (close the Unity Editor first):
  ```bash
  "/c/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Unity.exe" -batchmode -quit -projectPath . -executeMethod TownBuilder.EditorTools.BuildTools.BuildApkFromCommandLine -logFile Logs/build-apk.log
  ```

Install it on your phone: enable **Developer options → USB debugging**, connect the phone, then run
`adb install -r Builds/Android/<file>.apk`. You can also copy the APK to the phone and open it (allow "Install unknown apps").

## Google Play bundle (AAB)

### Step 1: create your upload key (once, by you)

Run this in **your own** terminal. It asks for a password and your name or organisation:

```powershell
mkdir $env:USERPROFILE\keys -Force
& "C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\keytool.exe" -genkeypair -v -keystore "$env:USERPROFILE\keys\townbuilder-upload.keystore" -alias upload -keyalg RSA -keysize 2048 -validity 10000
```

**Back up the `.keystore` file and its password** (password manager plus an offline copy). If you use
Play App Signing (the default), Google can reset a lost upload key, but that takes time. Keep the file outside this repository.

### Step 2: build the signed AAB

Close the Unity Editor, then:

```powershell
cd D:\01-Projects\05-Township\TownBuilder
powershell -ExecutionPolicy Bypass -File .\Tools\build-aab.ps1
```

The script asks for the passwords and passes them to Unity only for that build.

### Step 3: before the first upload

* Package name is **`com.pandian47.townbuilder`**. It is permanent once uploaded, so never change it.
* Raise **Bundle Version Code** (Player Settings → Android) by 1 for every upload, and the Version (e.g. 0.2.0).
  Version history: 0.1.0 (1) = foundation + test ads · 0.2.0 (2) = farming, barn, bakery · 0.3.0 (3) = orders and levels (v0.1 prototype) · 0.4.0 (4) = construction timers · 0.5.0 (5) = sound and music · 0.6.0 (6) = visual feedback effects · 0.7.0 (7) = TextMeshPro UI text · 0.8.0 (8) = first AI art set · 0.9.0 (9) = Feed Mill, Soybean, feeds · 0.10.0 (10) = Chicken Coop, Dairy Shed, eggs and milk · 0.11.0 (11) = Sugarcane, Sugar Mill · 0.12.0 (12) = Creamery, cookies, cake, butter, cheese · 0.13.0 (13) = factory queue upgrades, balance pass · 0.14.0 (14) = town tasks · 0.15.0 (15) = Train Station and train · 0.16.0 (16) = building materials · 0.17.0 (17) = land expansion · 0.18.0 (18) = Milestone 5 balance pass · 0.19.0 (19) = fixed railway and station, full AI art set · 0.19.1 (20) = developer mode in test APKs · 0.20.0 (21) = ambient life (crop sway, chimney smoke, floating badges) · 0.21.0 (22) = the train drives in and out · 0.22.0 (23) = townsfolk and cars on the roads · 0.23.0 (24) = UI polish (transitions, flying rewards, button squash) · 0.24.0 (25) = decorations (Milestone 6 complete) · 0.24.1 (26) = AI art for the Milestone 6 visuals · 0.24.2 (27) = one train at the platform, depth-sorting fix · 0.24.3 (28) = new Train Station art (platform without track) · 0.25.0 (29) = daily reward · 0.26.0 (30) = goals (achievements) · 0.27.0 (31) = orchard and Juice Press · 0.27.1–0.27.5 (32–36) = orchard art, Build window categories and cards, game font, top bar · 0.28.0 (37) = weekend events · 0.28.1 (38) = wooden Build window · 0.29.0 (39) = balance pass · 0.29.1 (40) = finish timers with gems · 0.30.0 (41) = delivery truck · 0.31.0 (42) = town happiness · 0.32.0 (43) = collections · 0.32.1 (44) = full-window Build categories · 0.33.0 (45) = seasons · 0.34.0 (46) = landscape painting · 0.34.1 (47) = 22 new art images · 0.34.2 (48) = new Train Station art · 0.35.0 (49) = launch prep: settings, credits, app icon, store screenshots · 0.35.1 (50) = owner's app icon and new train art · 0.35.2 (51) = task list opens from a Tasks button · 0.36.0 (52) = roads, paving and landscape tools (road routes, joined roads, more styles, eraser, undo) · 0.36.1 (53) = owner's art for the new road, paving and ground styles and tool cards · 0.36.2 (54) = bigger checker squares and meadow flowers (tile zoom) · 0.37.0 (55) = living Feed Mill (idle Z letters; working grain stream, dust, workers, rumble) · 0.38.0 (56) = all production buildings come alive · 0.38.1 (57) = idle Z letters in bursts of three with 5-10 s rests · 0.38.2 (58) = owner's new Feed Mill art (grain cart, conveyor, silo) · 0.38.3 (59) = zoom in closer (camera minimum size 1.6, was 2.5) · 0.39.0 (60) = phone back button and quit dialog · 0.40.0 (61) = rotate buildings (long press to move, Rotate button, mirrored facing, saved), new Feed Mill art, working buildings without workers or bounce · 0.40.1 (62) = ads switched off (all ad buttons hidden) · 0.40.2 (63) = oversized building pictures fit their plot · 0.41.0 (64) = new production window (wooden window, baskets, flying icons, factory arrows, Reduce motion) · 0.41.1 (65) = buildings no longer drawn tiny after a picture swap; products on their own tray, owner's baskets.

  **Developer mode:** test APKs (`BuildApk`) include the DEV button; the Play Store bundle (`build-aab.ps1` / `BuildAab`)
  is compiled without it.
* Play Console needs: a developer account ($25 one-time), an app listing, a privacy policy URL, the content-rating
  questionnaire, a data-safety form, screenshots, and a 512×512 icon. Start with an **Internal testing** track.
  **Step-by-step list: `ReleaseChecklist.md`** (privacy policy draft: `PrivacyPolicy.md`; store screenshots:
  `Documentation/Store/`; icon: `Assets/_Game/Art/AppIcon/AppIcon.png`).
* New personal developer accounts must run a closed test (12+ testers for 14 days) before production access.
* Target API level is set to "Automatic (highest installed)", which meets Google Play's current requirement.
