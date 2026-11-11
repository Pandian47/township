using UnityEngine;

namespace TownBuilder
{
    /// <summary>
    /// Ad unit ids and ad rules. The AdMob APP id is not here: it lives in
    /// Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset (menu Assets > Google Mobile Ads > Settings).
    ///
    /// Keep <see cref="useTestAds"/> ON during development. Showing/clicking real ads on your own device can get
    /// the AdMob account suspended.
    /// </summary>
    [CreateAssetMenu(menuName = "TownBuilder/Ads Config", fileName = "AdsConfig")]
    public sealed class AdsConfig : ScriptableObject
    {
        // Google's public sample ad units (always serve test ads).
        public const string GoogleTestRewardedAndroid = "ca-app-pub-3940256099942544/5224354917";
        public const string GoogleTestInterstitialAndroid = "ca-app-pub-3940256099942544/1033173712";
        public const string GoogleTestAppIdAndroid = "ca-app-pub-3940256099942544~3347511713";

        [SerializeField] private bool adsEnabled = true;
        [Tooltip("Use Google's sample ad units. Turn off only for the Play Store release build.")]
        [SerializeField] private bool useTestAds = true;

        [Header("Production ad units (Android) - from AdMob > Apps > Ad units")]
        [SerializeField] private string rewardedAdUnitAndroid = "";
        [SerializeField] private string interstitialAdUnitAndroid = "";

        [Header("Rules")]
        [SerializeField, Min(1)] private int rewardedCoins = 100;
        [Tooltip("Minimum seconds between two rewarded ads.")]
        [SerializeField, Min(0)] private float rewardedCooldownSeconds = 60f;
        [Tooltip("Minimum seconds between two interstitials (shown when leaving the town for the menu).")]
        [SerializeField, Min(0)] private float interstitialCooldownSeconds = 180f;
        [Tooltip("Set when the game targets children (changes consent and ad content rules).")]
        [SerializeField] private bool tagForUnderAgeOfConsent;

        [Header("Store listing")]
        [Tooltip("Web address of the hosted privacy policy (Documentation/PrivacyPolicy.md). Settings > Privacy policy opens it. "
                 + "The same address goes into Play Console > App content > Privacy policy.")]
        [SerializeField] private string privacyPolicyUrl = "";

        public bool AdsEnabled => adsEnabled;
        public bool UseTestAds => useTestAds;
        public int RewardedCoins => rewardedCoins;
        public float RewardedCooldownSeconds => rewardedCooldownSeconds;
        public float InterstitialCooldownSeconds => interstitialCooldownSeconds;
        public bool TagForUnderAgeOfConsent => tagForUnderAgeOfConsent;
        public string PrivacyPolicyUrl => privacyPolicyUrl;

        public string RewardedAdUnitId => useTestAds || string.IsNullOrEmpty(rewardedAdUnitAndroid)
            ? GoogleTestRewardedAndroid
            : rewardedAdUnitAndroid;

        public string InterstitialAdUnitId => useTestAds || string.IsNullOrEmpty(interstitialAdUnitAndroid)
            ? GoogleTestInterstitialAndroid
            : interstitialAdUnitAndroid;
    }
}
