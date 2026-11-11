# Google Play release checklist

Work through this list before the first Play Store upload (and skim it again for every update). Steps marked
**(you)** need your own accounts, passwords or decisions; nothing here should be done with someone else's keys.
Details for each step: `AndroidRelease.md` (builds and signing) and `Ads.md` (AdMob).

## 1. Accounts and keys

- [ ] **(you)** Google Play developer account (one-time fee) and an AdMob account linked to it.
- [ ] **(you)** Upload keystore created with `keytool` (AndroidRelease.md, Step 1), stored outside the repository, and
      backed up together with its password. Never commit it.

## 2. Ads: switch from test to real (only for the store build)

- [ ] **(you)** AdMob: create the app and two ad units (Rewarded, Interstitial).
- [ ] Unity: **Assets → Google Mobile Ads → Settings**: put your real **Android App ID**.
- [ ] `Assets/_Game/ScriptableObjects/AdsConfig`: paste the two real ad unit ids and **untick Use Test Ads**.
- [ ] **(you)** AdMob → Privacy & messaging: publish a **GDPR consent message** (and the US states message if offered).
- [ ] Add your phone as a **test device** in AdMob before trying the store build, and never tap your own real ads.
- [ ] After building the AAB, switch **Use Test Ads back on** for everyday test APKs.

## 3. Privacy policy

- [ ] Review and complete `PrivacyPolicy.md` (fill the brackets: name, contact email, date, age group).
- [ ] **(you)** Host it on a public page and copy the address.
- [ ] Paste the address into **AdsConfig → Privacy Policy Url** (Settings → Privacy policy opens it).
- [ ] Paste the same address into **Play Console → App content → Privacy policy**.

## 4. Build the bundle

- [ ] Raise the version: **Edit → Project Settings → Player → Android**: Version (e.g. 1.0.0) and **Bundle Version
      Code** +1 (every upload needs a higher code than the last one).
- [ ] Close Unity, run `Tools\build-aab.ps1` (asks for your keystore passwords) → `Builds/Android/*.aab`.
- [ ] The DEV button is **not** in the AAB (only test APKs include it). Check on an internal-test install.
- [ ] Install the internal-test build from Play on a real phone: new game, continue, ads, consent form, Settings,
      Credits, Privacy policy link.

## 5. Store listing (Play Console → Grow → Store presence → Main store listing)

- [ ] App name, short description (80 characters), full description (4000 characters): our own words.
- [ ] **App icon 512 × 512 PNG**: scale `Assets/_Game/Art/AppIcon/AppIcon.png` (1024 × 1024) down to 512 × 512.
- [ ] **Feature graphic 1024 × 500** (JPG or 24-bit PNG, no transparency): see the prompt in `ArtPrompts.md`.
- [ ] **Phone screenshots** (2 to 8): `Documentation/Store/Store_*.png` (1920 × 1080, made by
      `PreviewCapture.CaptureStoreFromCommandLine`). Replace them with real phone screenshots later if you like.
- [ ] Category (Games → Simulation or Casual), contact email, optional website.

## 6. App content (Play Console → Policy → App content)

- [ ] **Ads:** "Yes, my app contains ads".
- [ ] **Content rating questionnaire** (IARC): no violence, no gambling, no user chat, ads present.
- [ ] **Target audience and content**: choose the age groups. If you include children under 13, the Families policy
      applies and AdsConfig → Tag For Under Age Of Consent must be reviewed first; ask before changing it.
- [ ] **Data safety** form (based on the AdMob SDK, check Google's current AdMob data-disclosure page):
      collects *Device or other IDs* (advertising ID), *App interactions* / *Diagnostics* as Google's SDK lists them,
      used for *Advertising or marketing* and *Analytics*; data is encrypted in transit; no account, so no deletion
      request URL needed beyond uninstalling. The game's own saves stay on the device.
- [ ] **Advertising ID** declaration: "Yes, uses the advertising ID" (for ads). The AdMob SDK adds the
      `AD_ID` permission.
- [ ] Government apps / financial features / health: No.

## 7. Testing tracks

- [ ] Upload the AAB to **Internal testing** first; install it from Play on your phone.
- [ ] New personal developer accounts: run a **Closed test with at least 12 testers for 14 days** before you can apply
      for production.
- [ ] Then **Production** (start with a staged rollout, e.g. 20 %).

## 8. After each release

- [ ] Tag the commit (`git tag v1.0.0`) and add the version to the history in `AndroidRelease.md`.
- [ ] Keep the keystore backup safe; never commit `.keystore`, `.aab` or `.apk` files.
