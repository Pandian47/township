using System;

namespace TownBuilder
{
    /// <summary>
    /// Game-facing ads API. Gameplay code only talks to this interface, never to the AdMob SDK, so the game runs
    /// identically with ads disabled, in the Editor and in tests. All callbacks arrive on the Unity main thread.
    /// </summary>
    public interface IAdService
    {
        /// <summary>Raised when ad readiness or privacy-button state changes (refresh UI).</summary>
        event Action StateChanged;

        bool IsRewardedReady { get; }

        /// <summary>True when the user must be able to reopen the consent form (GDPR regions).</summary>
        bool PrivacyOptionsRequired { get; }

        /// <summary>Shows a rewarded ad. <paramref name="onFinished"/> receives true only if the reward was earned.</summary>
        void ShowRewarded(Action<bool> onFinished);

        /// <summary>Shows an interstitial if one is loaded and the cooldown passed; always calls <paramref name="onClosed"/>.</summary>
        void ShowInterstitialIfAllowed(Action onClosed);

        void ShowPrivacyOptions(Action onClosed);
    }

    /// <summary>Used when ads are disabled.</summary>
    public sealed class NoAdService : IAdService
    {
        public event Action StateChanged { add { } remove { } }
        public bool IsRewardedReady => false;
        public bool PrivacyOptionsRequired => false;
        public void ShowRewarded(Action<bool> onFinished) => onFinished?.Invoke(false);
        public void ShowInterstitialIfAllowed(Action onClosed) => onClosed?.Invoke();
        public void ShowPrivacyOptions(Action onClosed) => onClosed?.Invoke();
    }

    /// <summary>Editor/test stand-in: rewarded ads are always ready and grant instantly (with a log line).</summary>
    public sealed class SimulatedAdService : IAdService
    {
        public event Action StateChanged;
        public bool IsRewardedReady => true;
        public bool PrivacyOptionsRequired => false;

        public void ShowRewarded(Action<bool> onFinished)
        {
            UnityEngine.Debug.Log("[Ads] (simulated) rewarded ad watched");
            onFinished?.Invoke(true);
            StateChanged?.Invoke();
        }

        public void ShowInterstitialIfAllowed(Action onClosed)
        {
            UnityEngine.Debug.Log("[Ads] (simulated) interstitial");
            onClosed?.Invoke();
        }

        public void ShowPrivacyOptions(Action onClosed) => onClosed?.Invoke();
    }
}
