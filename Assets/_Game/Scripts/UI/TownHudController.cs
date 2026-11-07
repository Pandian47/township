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

        [Header("Bottom navigation")]
        [SerializeField] private GameObject bottomNavigation;
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
        private Wallet _wallet;

        public void Initialize(PlacementSystem placement, Wallet wallet, PlayerProgress progress, GameContent content,
            IAdService ads, Action onMenu)
        {
            _placement = placement;
            _wallet = wallet;

            topBar.Bind(wallet, progress);

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
        }

        private void AskRemove()
        {
            if (!_placement.TryGetTargetDefinition(out var definition)) return;
            confirmDialog.Show($"Remove {definition.DisplayName}?\nThis cannot be undone and coins are not refunded.", "Remove",
                () => _placement.RemoveSelected());
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
