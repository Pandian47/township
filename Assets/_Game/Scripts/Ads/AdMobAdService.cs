using System;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace TownBuilder
{
    /// <summary>
    /// Google AdMob implementation (device builds).
    /// Start-up order required by Google: 1) update consent (UMP, shows the GDPR form where required)
    /// 2) MobileAds.Initialize only if ads may be requested 3) preload rewarded + interstitial.
    /// Ads are reloaded after every show and retried with back-off after a failed load.
    /// </summary>
    public sealed class AdMobAdService : IAdService
    {
        private const float MaxRetryDelaySeconds = 64f;

        private readonly AdsConfig _config;
        private readonly CoroutineRunner _runner;
        private RewardedAd _rewarded;
        private InterstitialAd _interstitial;
        private bool _initialized;
        private bool _loadingRewarded;
        private bool _loadingInterstitial;
        private float _rewardedRetryDelay = 2f;
        private float _interstitialRetryDelay = 2f;
        private float _lastRewardedShownAt = float.NegativeInfinity;
        private float _lastInterstitialShownAt = float.NegativeInfinity;

        public AdMobAdService(AdsConfig config, CoroutineRunner runner)
        {
            _config = config;
            _runner = runner;
        }

        public event Action StateChanged;

        public bool IsRewardedReady =>
            _initialized && _rewarded != null && _rewarded.CanShowAd() &&
            Time.realtimeSinceStartup - _lastRewardedShownAt >= _config.RewardedCooldownSeconds;

        public bool PrivacyOptionsRequired =>
            ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

        public void Initialize()
        {
            // Deliver every ad callback on the main thread so game code can touch Unity objects safely.
            MobileAds.RaiseAdEventsOnUnityMainThread = true;

            var parameters = new ConsentRequestParameters { TagForUnderAgeOfConsent = _config.TagForUnderAgeOfConsent };
            ConsentInformation.Update(parameters, updateError =>
            {
                if (updateError != null) Debug.LogWarning($"[Ads] Consent update failed: {updateError.Message}");

                ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
                {
                    if (formError != null) Debug.LogWarning($"[Ads] Consent form: {formError.Message}");
                    StateChanged?.Invoke();
                    if (ConsentInformation.CanRequestAds()) InitializeSdk();
                    else Debug.Log("[Ads] Consent not given; ads stay off.");
                });
            });

            // Returning players with consent from a previous session can start immediately.
            if (ConsentInformation.CanRequestAds()) InitializeSdk();
        }

        public void ShowRewarded(Action<bool> onFinished)
        {
            if (!IsRewardedReady)
            {
                onFinished?.Invoke(false);
                return;
            }

            bool earned = false;
            var ad = _rewarded;
            _rewarded = null;
            _lastRewardedShownAt = Time.realtimeSinceStartup;

            ad.OnAdFullScreenContentClosed += () =>
            {
                ad.Destroy();
                onFinished?.Invoke(earned);
                LoadRewarded();
                StateChanged?.Invoke();
            };
            ad.OnAdFullScreenContentFailed += error =>
            {
                Debug.LogWarning($"[Ads] Rewarded failed to show: {error.GetMessage()}");
                ad.Destroy();
                onFinished?.Invoke(false);
                LoadRewarded();
                StateChanged?.Invoke();
            };
            ad.Show(reward => earned = true);
            StateChanged?.Invoke();
        }

        public void ShowInterstitialIfAllowed(Action onClosed)
        {
            bool cooldownOver = Time.realtimeSinceStartup - _lastInterstitialShownAt >= _config.InterstitialCooldownSeconds;
            if (!_initialized || _interstitial == null || !_interstitial.CanShowAd() || !cooldownOver)
            {
                onClosed?.Invoke();
                return;
            }

            var ad = _interstitial;
            _interstitial = null;
            _lastInterstitialShownAt = Time.realtimeSinceStartup;
            ad.OnAdFullScreenContentClosed += () =>
            {
                ad.Destroy();
                LoadInterstitial();
                onClosed?.Invoke();
            };
            ad.OnAdFullScreenContentFailed += error =>
            {
                ad.Destroy();
                LoadInterstitial();
                onClosed?.Invoke();
            };
            ad.Show();
        }

        public void ShowPrivacyOptions(Action onClosed)
        {
            ConsentForm.ShowPrivacyOptionsForm(error =>
            {
                if (error != null) Debug.LogWarning($"[Ads] Privacy options: {error.Message}");
                StateChanged?.Invoke();
                if (ConsentInformation.CanRequestAds()) InitializeSdk();
                onClosed?.Invoke();
            });
        }

        // ------------------------------------------------------------------

        private void InitializeSdk()
        {
            if (_initialized) return;
            _initialized = true;
            MobileAds.Initialize(status =>
            {
                Debug.Log($"[Ads] Google Mobile Ads initialised ({(_config.UseTestAds ? "TEST ads" : "LIVE ads")}).");
                LoadRewarded();
                LoadInterstitial();
            });
        }

        private void LoadRewarded()
        {
            if (_loadingRewarded || _rewarded != null) return;
            _loadingRewarded = true;
            RewardedAd.Load(_config.RewardedAdUnitId, new AdRequest(), (ad, error) =>
            {
                _loadingRewarded = false;
                if (error != null || ad == null)
                {
                    Debug.LogWarning($"[Ads] Rewarded load failed: {error?.GetMessage()}; retry in {_rewardedRetryDelay}s");
                    _runner.RunAfter(_rewardedRetryDelay, LoadRewarded);
                    _rewardedRetryDelay = Mathf.Min(_rewardedRetryDelay * 2f, MaxRetryDelaySeconds);
                    return;
                }

                _rewardedRetryDelay = 2f;
                _rewarded = ad;
                StateChanged?.Invoke();
            });
        }

        private void LoadInterstitial()
        {
            if (_loadingInterstitial || _interstitial != null) return;
            _loadingInterstitial = true;
            InterstitialAd.Load(_config.InterstitialAdUnitId, new AdRequest(), (ad, error) =>
            {
                _loadingInterstitial = false;
                if (error != null || ad == null)
                {
                    _runner.RunAfter(_interstitialRetryDelay, LoadInterstitial);
                    _interstitialRetryDelay = Mathf.Min(_interstitialRetryDelay * 2f, MaxRetryDelaySeconds);
                    return;
                }

                _interstitialRetryDelay = 2f;
                _interstitial = ad;
            });
        }
    }
}
