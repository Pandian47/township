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
  Version history: 0.1.0 (1) = foundation + test ads · 0.2.0 (2) = farming, barn, bakery · 0.3.0 (3) = orders and levels (v0.1 prototype) · 0.4.0 (4) = construction timers · 0.5.0 (5) = sound and music · 0.6.0 (6) = visual feedback effects · 0.7.0 (7) = TextMeshPro UI text · 0.8.0 (8) = first AI art set · 0.9.0 (9) = Feed Mill, Soybean, feeds · 0.10.0 (10) = Chicken Coop, Dairy Shed, eggs and milk · 0.11.0 (11) = Sugarcane, Sugar Mill · 0.12.0 (12) = Creamery, cookies, cake, butter, cheese · 0.13.0 (13) = factory queue upgrades, balance pass · 0.14.0 (14) = town tasks · 0.15.0 (15) = Train Station and train · 0.16.0 (16) = building materials · 0.17.0 (17) = land expansion · 0.18.0 (18) = Milestone 5 balance pass · 0.19.0 (19) = fixed railway and station, full AI art set · 0.19.1 (20) = developer mode in test APKs · 0.20.0 (21) = ambient life (crop sway, chimney smoke, floating badges) · 0.21.0 (22) = the train drives in and out · 0.22.0 (23) = townsfolk and cars on the roads · 0.23.0 (24) = UI polish (transitions, flying rewards, button squash) · 0.24.0 (25) = decorations (Milestone 6 complete) · 0.24.1 (26) = AI art for the Milestone 6 visuals · 0.24.2 (27) = one train at the platform, depth-sorting fix · 0.24.3 (28) = new Train Station art (platform without track).

  **Developer mode:** test APKs (`BuildApk`) include the DEV button; the Play Store bundle (`build-aab.ps1` / `BuildAab`)
  is compiled without it.
* Play Console needs: a developer account ($25 one-time), an app listing, a privacy policy URL, the content-rating
  questionnaire, a data-safety form, screenshots, and a 512×512 icon. Start with an **Internal testing** track.
* New personal developer accounts must run a closed test (12+ testers for 14 days) before production access.
* Target API level is set to "Automatic (highest installed)", which meets Google Play's current requirement.
