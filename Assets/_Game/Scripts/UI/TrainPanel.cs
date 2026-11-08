using TMPro;
using TownBuilder.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace TownBuilder
{
    /// <summary>
    /// Shown above the selection bar while the Train Station is selected. Train here: crates to load + "Send train".
    /// Train away: time until it's back + "Call now (watch ad)".
    /// </summary>
    public sealed class TrainPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private GameObject cratesRow;
        [SerializeField] private TrainCrateView[] crates = new TrainCrateView[0];
        [SerializeField] private Button sendButton;
        [SerializeField] private TMP_Text sendLabel;
        [SerializeField] private Button callButton;
        [SerializeField] private TMP_Text callLabel;

        private TrainController _train;
        private GameContent _content;
        private Inventory _inventory;
        private IAdService _ads;
        private float _nextRefresh;

        public bool IsOpen => panelRoot.activeSelf;

        public void Initialize(TrainController train, GameContent content, Inventory inventory, IAdService ads)
        {
            _train = train;
            _content = content;
            _inventory = inventory;
            _ads = ads;
            sendButton.onClick.AddListener(() =>
            {
                _train.SendOff();
                Refresh();
            });
            callButton.onClick.AddListener(OnCall);
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
            if (_train == null) return;
            var train = _train.Train;
            titleText.text = "Train Station";
            if (!_train.IsOpen)
            {
                cratesRow.SetActive(false);
                sendButton.gameObject.SetActive(false);
                callButton.gameObject.SetActive(false);
                statusText.text = $"Opens at level {_train.UnlockLevel}. The first train comes right away.";
                return;
            }

            bool here = train.IsAtStation;
            cratesRow.SetActive(here);
            sendButton.gameObject.SetActive(here);

            bool adsAvailable = !(_ads is NoAdService);
            callButton.gameObject.SetActive(!here && adsAvailable);

            if (!here)
            {
                statusText.text = $"The train is away. Back in {FieldPanel.FormatDuration(_train.RemainingMs)}";
                callButton.interactable = _ads.IsRewardedReady;
                callLabel.text = _ads.IsRewardedReady ? "Call now\n(watch ad)" : "Ad loading...";
                return;
            }

            statusText.text = $"Loaded {train.FilledCount}/{train.Crates.Count}";
            for (int i = 0; i < crates.Length; i++)
            {
                if (i >= train.Crates.Count) { crates[i].Hide(); continue; }
                var crate = train.Crates[i];
                _content.TryGetItem(crate.ItemId, out var item);
                int index = i;
                string material = crate.MaterialId != null ? $"+{crate.MaterialAmount} {_content.ItemName(crate.MaterialId)}" : null;
                crates[i].Show(item != null ? item.Icon : null, _content.ItemName(crate.ItemId), crate.Amount,
                    _inventory.Count(crate.ItemId), crate.RewardCoins, crate.RewardXp, crate.Filled, () =>
                    {
                        _train.Fill(index);
                        Refresh();
                    }, material);
            }

            sendLabel.text = train.AllFilled ? $"Send train\n+{_train.FullTrainBonus:N0} bonus" : "Send train\n(no bonus)";
        }

        private void OnCall()
        {
            callButton.interactable = false;
            _ads.ShowRewarded(earned =>
            {
                if (earned) _train.CallNow();
                Refresh();
            });
        }
    }
}
