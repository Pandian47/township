using System.Collections.Generic;
using TMPro;
using TownBuilder.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace TownBuilder
{
    /// <summary>
    /// Shown above the selection bar while the Truck Depot is selected. Truck parked: three load cards (two item lines
    /// each, reward, Load button) + "Send truck". Truck away: time left, what it brings back, Call back (ad / gems).
    /// </summary>
    public sealed class TruckPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private GameObject loadsRow;
        [SerializeField] private TruckLoadView[] loads = new TruckLoadView[0];
        [SerializeField] private Button sendButton;
        [SerializeField] private TMP_Text sendLabel;
        [SerializeField] private Button callButton;
        [SerializeField] private TMP_Text callLabel;
        [SerializeField] private Button gemsButton;
        [SerializeField] private TMP_Text gemsLabel;

        private TruckController _truck;
        private GameContent _content;
        private Inventory _inventory;
        private IAdService _ads;
        private float _nextRefresh;

        public bool IsOpen => panelRoot.activeSelf;

        public void Initialize(TruckController truck, GameContent content, Inventory inventory, IAdService ads)
        {
            _truck = truck;
            _content = content;
            _inventory = inventory;
            _ads = ads;
            sendButton.onClick.AddListener(() =>
            {
                _truck.SendOff();
                Refresh();
            });
            callButton.onClick.AddListener(() =>
            {
                callButton.interactable = false;
                _ads.ShowRewarded(earned =>
                {
                    if (earned) _truck.CallBack();
                    Refresh();
                });
            });
            gemsButton.onClick.AddListener(() =>
            {
                _truck.CallBackWithGems();
                Refresh();
            });
            Hide();
        }

        public void Show()
        {
            if (IsOpen) return;
            panelRoot.SetActive(true);
            Refresh();
        }

        public void Hide() => panelRoot.SetActive(false);

        private void Update()
        {
            if (!IsOpen || Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.25f;
            Refresh();
        }

        private void Refresh()
        {
            if (_truck == null || _truck.Truck == null) return;
            var truck = _truck.Truck;
            bool here = truck.IsAtDepot;
            loadsRow.SetActive(here);
            sendButton.gameObject.SetActive(here);

            if (!here)
            {
                var cargo = truck.Cargo;
                statusText.text = $"On the road. Back in {FieldPanel.FormatDuration(_truck.RemainingMs)} with {cargo.Coins:N0} coins and {cargo.Xp} XP";
                bool adsAvailable = !(_ads is NoAdService);
                callButton.gameObject.SetActive(adsAvailable);
                callButton.interactable = _ads.IsRewardedReady;
                callLabel.text = _ads.IsRewardedReady ? "Call back\n(watch ad)" : "Ad loading...";
                int gems = _truck.GemCostToCallBack;
                gemsButton.gameObject.SetActive(gems > 0);
                gemsLabel.text = $"Call back\n{gems} gem{(gems == 1 ? "" : "s")}";
                gemsButton.interactable = _truck.HasGems(gems);
                return;
            }

            callButton.gameObject.SetActive(false);
            gemsButton.gameObject.SetActive(false);
            statusText.text = $"Loaded {truck.LoadedCount}/{truck.Loads.Count}. Earnings are paid when the truck comes back.";
            for (int i = 0; i < loads.Length; i++)
            {
                if (i >= truck.Loads.Count)
                {
                    loads[i].Hide();
                    continue;
                }

                var load = truck.Loads[i];
                var lines = new List<(Sprite icon, string text, bool enough)>();
                foreach (var line in load.Lines)
                {
                    _content.TryGetItem(line.ItemId, out var item);
                    int have = _inventory.Count(line.ItemId);
                    lines.Add((item != null ? item.Icon : null, $"{_content.ItemName(line.ItemId)}  {Mathf.Min(have, line.Amount)}/{line.Amount}",
                        have >= line.Amount));
                }

                string reward = $"+{load.RewardCoins:N0} coins  +{load.RewardXp} XP";
                if (load.MaterialId != null) reward += $"\n+{load.MaterialAmount} {_content.ItemName(load.MaterialId)}";
                int index = i;
                loads[i].Show(lines, reward, load.Loaded, _truck.HasItemsFor(i), () =>
                {
                    _truck.Load(index);
                    Refresh();
                });
            }

            sendButton.interactable = truck.CanSend;
            sendLabel.text = truck.AllLoaded ? $"Send truck\n+{_truck.FullBonus:N0} bonus" : "Send truck";
        }
    }
}
