using System;
using TownBuilder.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace TownBuilder
{
    /// <summary>Town scene UI: top bar, bottom navigation, shop, placement and selection action bars.</summary>
    public sealed class TownHudController : MonoBehaviour
    {
        [SerializeField] private TopBarView topBar;
        [SerializeField] private BuildingShopPanel shopPanel;
        [SerializeField] private ConfirmDialog confirmDialog;

        [Header("Ads")]
        [SerializeField] private RewardedCoinsButton rewardedCoinsButton;

        [Header("Farming")]
        [SerializeField] private FieldPanel fieldPanel;
        [SerializeField] private ToastView toast;

        [Header("Barn")]
        [SerializeField] private BarnPanel barnPanel;
        [Tooltip("The barn pill in the top bar also opens the barn.")]
        [SerializeField] private Button barnPillButton;

        [Header("Bottom navigation")]
        [SerializeField] private GameObject bottomNavigation;
        [SerializeField] private Button barnButton;
        [SerializeField] private Button buildButton;
        [SerializeField] private Button menuButton;

        [Header("Placement bar (placing / moving)")]
        [SerializeField] private GameObject placementBar;
        [SerializeField] private Text placementInfoText;
        [SerializeField] private Button confirmPlacementButton;
        [SerializeField] private Button cancelPlacementButton;

        [Header("Selection bar")]
        [SerializeField] private GameObject selectionBar;
        [SerializeField] private Text selectionNameText;
        [SerializeField] private Button moveButton;
        [SerializeField] private Button removeButton;
        [SerializeField] private Button deselectButton;

        private PlacementSystem _placement;
        private FarmingController _farming;
        private StorageService _storage;
        private Wallet _wallet;

        public void Initialize(PlacementSystem placement, FarmingController farming, StorageService storage, Wallet wallet,
            PlayerProgress progress, GameContent content, IAdService ads, Action onMenu)
        {
            _placement = placement;
            _farming = farming;
            _storage = storage;
            _wallet = wallet;

            topBar.Bind(wallet, progress, storage.Inventory);
            fieldPanel.Initialize(farming, content, wallet, ads, onPlanted: () => _placement.Cancel());
            farming.Notification += toast.Show;
            storage.Notification += toast.Show;

            barnPanel.Initialize(storage, content);
            barnButton.onClick.AddListener(OpenBarn);
            barnPillButton.onClick.AddListener(OpenBarn);

            bool adsOn = content.AdsConfig != null && content.AdsConfig.AdsEnabled && !(ads is NoAdService);
            rewardedCoinsButton.gameObject.SetActive(adsOn);
            if (adsOn) rewardedCoinsButton.Initialize(ads, wallet, content.AdsConfig.RewardedCoins);
            shopPanel.Initialize(content, wallet, progress, _placement.CanBuildMore, d => _placement.BeginPlacement(d));

            buildButton.onClick.AddListener(() =>
            {
                _placement.Cancel();
                shopPanel.Open();
            });
            menuButton.onClick.AddListener(() => onMenu?.Invoke());

            confirmPlacementButton.onClick.AddListener(() => _placement.Confirm());
            cancelPlacementButton.onClick.AddListener(() => _placement.Cancel());

            moveButton.onClick.AddListener(() => _placement.BeginMoveSelected());
            deselectButton.onClick.AddListener(() => _placement.Cancel());
            removeButton.onClick.AddListener(AskRemove);

            _placement.StateChanged += Refresh;
            _wallet.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_placement != null) _placement.StateChanged -= Refresh;
            if (_wallet != null) _wallet.Changed -= Refresh;
            if (_farming != null) _farming.Notification -= toast.Show;
            if (_storage != null) _storage.Notification -= toast.Show;
        }

        private void OpenBarn()
        {
            if (_placement.CurrentMode == PlacementSystem.Mode.PlacingNew || _placement.CurrentMode == PlacementSystem.Mode.Moving) return;
            _placement.Cancel();
            barnPanel.Open();
        }

        private void AskRemove()
        {
            if (!_placement.TryGetTargetDefinition(out var definition)) return;
            bool hasCrop = _farming.GetStage(_placement.TargetInstanceId) != PlotStage.Empty;
            string warning = hasCrop ? "\nThe crop growing on it will be lost." : "\nThis cannot be undone and coins are not refunded.";
            confirmDialog.Show($"Remove {definition.DisplayName}?{warning}", "Remove", () => _placement.RemoveSelected());
        }

        private void Refresh()
        {
            var mode = _placement.CurrentMode;
            bool placing = mode == PlacementSystem.Mode.PlacingNew || mode == PlacementSystem.Mode.Moving;
            bool selected = mode == PlacementSystem.Mode.Selected;

            bottomNavigation.SetActive(!placing && !selected); // also hides the rewarded-ad button inside it
            placementBar.SetActive(placing);
            selectionBar.SetActive(selected);

            _placement.TryGetTargetDefinition(out var definition);

            if (placing)
            {
                confirmPlacementButton.interactable = _placement.CanConfirm;
                placementInfoText.text = BuildPlacementInfo(mode, definition);
            }

            if (selected && definition != null)
            {
                selectionNameText.text = definition.DisplayName;
                removeButton.interactable = definition.Removable;
            }

            bool fieldSelected = selected && definition != null && definition.IsFarmPlot;
            if (fieldSelected && fieldPanel.PlotId != _placement.TargetInstanceId) fieldPanel.ShowFor(_placement.TargetInstanceId);
            else if (!fieldSelected && fieldPanel.PlotId != null) fieldPanel.Hide();
        }

        private string BuildPlacementInfo(PlacementSystem.Mode mode, BuildingDefinition definition)
        {
            if (definition == null) return string.Empty;
            string state = _placement.PreviewValidity switch
            {
                PlacementResult.Valid => "Drag or tap to position",
                PlacementResult.Overlap => "Blocked by another building",
                PlacementResult.BlockedTerrain => "Can't build here",
                PlacementResult.OutOfBounds => "Outside the town",
                _ => _placement.PreviewValidity.ToString(),
            };

            if (mode == PlacementSystem.Mode.PlacingNew)
            {
                string cost = _placement.CanAffordActive ? $"{definition.CoinCost:N0} coins" : $"Need {definition.CoinCost:N0} coins";
                return $"{definition.DisplayName}  -  {cost}\n{state}";
            }

            return $"Moving {definition.DisplayName}\n{state}";
        }
    }
}
