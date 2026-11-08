using System;
using TownBuilder.Domain;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TownBuilder
{
    /// <summary>"Watch ad: +N coins" button. Only clickable when a rewarded ad is ready; coins go through the Wallet.</summary>
    public sealed class RewardedCoinsButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;

        private IAdService _ads;
        private Wallet _wallet;
        private int _coins;
        private bool _showing;
        private float _nextRefresh;

        public event Action RewardGranted;

        public void Initialize(IAdService ads, Wallet wallet, int coins)
        {
            _ads = ads;
            _wallet = wallet;
            _coins = coins;
            button.onClick.AddListener(OnClicked);
            _ads.StateChanged += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_ads != null) _ads.StateChanged -= Refresh;
        }

        // Cooldowns expire with time, so poll lightly as well.
        private void Update()
        {
            if (_ads == null || Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.5f;
            Refresh();
        }

        private void OnClicked()
        {
            if (_showing || !_ads.IsRewardedReady) return;
            _showing = true;
            Refresh();
            _ads.ShowRewarded(earned =>
            {
                _showing = false;
                if (earned)
                {
                    _wallet.AddCoins(_coins);
                    RewardGranted?.Invoke();
                }

                Refresh();
            });
        }

        private void Refresh()
        {
            if (_ads == null || button == null) return;
            bool ready = _ads.IsRewardedReady && !_showing;
            button.interactable = ready;
            label.text = ready ? $"Watch ad\n+{_coins} coins" : "Ad loading...";
        }
    }
}
