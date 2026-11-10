using System;
using System.Collections.Generic;
using TownBuilder.Domain;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TownBuilder
{
    /// <summary>
    /// Shown above the selection bar while a field or fruit tree is selected (a tree is never empty: its fruit regrows).
    ///   Empty field   -> crop buttons (tap to plant).
    ///   Growing field -> time left, progress bar, "Finish now" (rewarded ad).
    ///   Ready field   -> "Harvest" button (tapping the field itself also harvests).
    /// </summary>
    public sealed class FieldPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text titleText;

        [Header("Empty field: crop picker")]
        [SerializeField] private GameObject cropsView;
        [SerializeField] private Transform cropsParent;
        [SerializeField] private CropButtonView cropTemplate;

        [Header("Growing / ready field")]
        [SerializeField] private GameObject growingView;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Image progressFill;
        [SerializeField] private Button actionButton;
        [SerializeField] private TMP_Text actionLabel;

        private readonly List<CropButtonView> _cropButtons = new List<CropButtonView>();
        private FarmingController _farming;
        private GameContent _content;
        private Wallet _wallet;
        private IAdService _ads;
        private Action _onPlanted;
        private string _plotId;
        private PlotStage _shownStage;
        private float _nextRefresh;

        public string PlotId => _plotId;

        public void Initialize(FarmingController farming, GameContent content, Wallet wallet, IAdService ads, Action onPlanted)
        {
            _farming = farming;
            _content = content;
            _wallet = wallet;
            _ads = ads;
            _onPlanted = onPlanted;

            cropTemplate.gameObject.SetActive(false);
            foreach (var crop in content.Crops)
            {
                if (crop == null || crop.GrowsOnTree) continue; // tree fruit isn't planted on fields
                var button = Instantiate(cropTemplate, cropsParent);
                button.gameObject.SetActive(true);
                button.Bind(crop, OnCropClicked);
                _cropButtons.Add(button);
            }

            actionButton.onClick.AddListener(OnActionClicked);
            Hide();
        }

        public void ShowFor(string plotId)
        {
            _plotId = plotId;
            panelRoot.SetActive(true);
            _farming.IsGrowingSpot(plotId, out var def);
            titleText.text = def != null ? def.DisplayName : "Field";
            Refresh(force: true);
        }

        public void Hide()
        {
            _plotId = null;
            panelRoot.SetActive(false);
        }

        private void Update()
        {
            if (_plotId == null || Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.2f;
            Refresh(force: false);
        }

        private void Refresh(bool force)
        {
            if (_farming.GetStage(_plotId) == PlotStage.Empty) _farming.EnsureTreeGrowing(_plotId);
            var stage = _farming.GetStage(_plotId);
            if (force || stage != _shownStage)
            {
                _shownStage = stage;
                cropsView.SetActive(stage == PlotStage.Empty);
                growingView.SetActive(stage != PlotStage.Empty);
            }

            if (stage == PlotStage.Empty)
            {
                foreach (var b in _cropButtons)
                    b.Refresh(_farming.IsUnlocked(b.Crop), _wallet.CanAffordCoins(b.Crop.SeedCoinCost));
                return;
            }

            _farming.Plots.TryGetCrop(_plotId, out var state);
            _content.TryGetCrop(state.CropId, out var crop);
            string cropName = crop != null ? crop.DisplayName : state.CropId;
            long now = _farming.NowUnixMs;
            progressFill.fillAmount = state.Progress(now);

            if (stage == PlotStage.Ready)
            {
                statusText.text = $"{cropName} is ready!";
                actionLabel.text = "Harvest";
                actionButton.gameObject.SetActive(true);
                actionButton.interactable = true;
            }
            else
            {
                statusText.text = $"{cropName}: {FormatDuration(state.RemainingMs(now))} left";
                bool adsAvailable = !(_ads is NoAdService);
                actionButton.gameObject.SetActive(adsAvailable);
                actionButton.interactable = _ads.IsRewardedReady;
                actionLabel.text = _ads.IsRewardedReady ? "Finish now\n(watch ad)" : "Ad loading...";
            }
        }

        private void OnCropClicked(CropDefinition crop)
        {
            if (_plotId == null) return;
            if (_farming.Plant(_plotId, crop) == PlantResult.Planted) _onPlanted?.Invoke();
            else Refresh(force: true);
        }

        private void OnActionClicked()
        {
            if (_plotId == null) return;
            string plotId = _plotId;
            if (_farming.GetStage(plotId) == PlotStage.Ready)
            {
                _farming.TryHarvest(plotId);
                Refresh(force: true);
                return;
            }

            actionButton.interactable = false;
            _ads.ShowRewarded(earned =>
            {
                if (earned) _farming.FinishNow(plotId);
                if (_plotId != null) Refresh(force: true);
            });
        }

        public static string FormatDuration(long milliseconds)
        {
            var t = TimeSpan.FromMilliseconds(Math.Max(0, milliseconds));
            if (t.TotalHours >= 1) return $"{(int)t.TotalHours}h {t.Minutes:D2}m";
            if (t.TotalMinutes >= 1) return $"{t.Minutes}m {t.Seconds:D2}s";
            return $"{Math.Max(1, (int)Math.Ceiling(t.TotalSeconds))}s";
        }
    }
}
