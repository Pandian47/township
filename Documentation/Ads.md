# Ads (Google AdMob)

Plugin: **Google Mobile Ads Unity 11.6.0** (OpenUPM `com.google.ads.mobile`) with Unity External Dependency Manager.
Everything currently uses Google's **test** ids, so only test ads are shown.

> **Ads are switched off (v0.40.1).** `AdsConfig > Ads Enabled` is unticked, so the game uses `NoAdService`: every
> "watch ad" button is hidden, Menu shows no interstitial and the AdMob SDK is never started (no consent form).
> The plugin stays in the project. To bring ads back: tick **Ads Enabled** in `ScriptableObjects/AdsConfig.asset`
> (Project window → select it → Inspector), save, rebuild.

## What's in the game

| Ad | Where | Rule (AdsConfig) |
|---|---|---|
| Rewarded video | "Watch ad +100 coins" button in the Town bottom bar | coins = `rewardedCoins` (100); 60 s cooldown |
| Interstitial | When you press **Menu** in the Town (natural break) | at most once per 180 s |
| Consent (UMP) | First launch in EEA/UK/Switzerland. **Settings → Privacy & ads** reopens it | Required by Google |

In the **Unity Editor**, a `SimulatedAdService` grants rewards instantly, so the real SDK never runs and tests stay offline.
Real ads appear only in the Android build.

## Code map

| File | Role |
|---|---|
| `Scripts/Ads/IAdService.cs` | Game-facing interface plus `NoAdService` and `SimulatedAdService` |
| `Scripts/Ads/AdMobAdService.cs` | Real implementation: consent → `MobileAds.Initialize` → preload → show → reload (with retry back-off) |
| `Scripts/Ads/AdsConfig.cs` | ScriptableObject holding ad unit ids, test switch, rewards and cooldowns |
| `Scripts/Ads/RewardedCoinsButton.cs` | HUD button. Coins go through `Wallet` and are autosaved |
| `ScriptableObjects/AdsConfig.asset` | The values (edit in the Inspector) |
| `Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset` | **AdMob App ID** (menu Assets → Google Mobile Ads → Settings) |

## Going live (do this only for the Play Store release)

The full release list (privacy policy, data safety, store listing) is in `ReleaseChecklist.md`.

1. In AdMob, create the app and two ad units: Rewarded and Interstitial.
2. **Assets → Google Mobile Ads → Settings → Android App ID**: paste your real App ID (`ca-app-pub-…~…`).
3. Open `ScriptableObjects/AdsConfig.asset`: paste the two ad unit ids (`ca-app-pub-…/…`) and untick **Use Test Ads**.
4. In AdMob → Privacy & messaging, create a **GDPR consent message** for the app. The UMP form shows it.
5. Play Console → App content: set **Contains ads = Yes**, and declare Advertising ID use in **Data safety**.
6. Keep testing your release build with your phone registered as a **test device** in AdMob. Never tap live ads on your own device.

## Rules

* Gameplay code calls `IAdService` only, never `GoogleMobileAds.*` directly.
* Rewards are applied only in the `ShowRewarded` callback when `earned == true`.
* When cloud saves exist, switch rewarded ads to server-side verification (SSV) before giving premium currency.
