using System;
using TownBuilder.Domain;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TownBuilder
{
    /// <summary>Town scene UI: top bar, bottom navigation, shop, placement and selection action bars.</summary>
    public sealed class TownHudController : MonoBehaviour
    {
        [SerializeField] private TopBarView topBar;
        [SerializeField] private BuildingShopPanel shopPanel;
        [SerializeField] private ConfirmDialog confirmDialog;
        [SerializeField] private TasksPanel tasksPanel;

        [Header("Ads")]
        [SerializeField] private RewardedCoinsButton rewardedCoinsButton;

        [Header("Farming")]
        [SerializeField] private FieldPanel fieldPanel;
        [SerializeField] private ToastView toast;

        [Header("Construction")]
        [SerializeField] private ConstructionPanel constructionPanel;

        [Header("Production")]
        [SerializeField] private FactoryPanel factoryPanel;

        [Header("Orders and levels")]
        [SerializeField] private OrdersPanel ordersPanel;
        [SerializeField] private Button ordersButton;
        [SerializeField] private LevelUpDialog levelUpDialog;

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
        [SerializeField] private TMP_Text placementInfoText;
        [SerializeField] private Button confirmPlacementButton;
        [SerializeField] private Button cancelPlacementButton;

        [Header("Selection bar")]
        [SerializeField] private GameObject selectionBar;
        [SerializeField] private TMP_Text selectionNameText;
        [SerializeField] private Button moveButton;
        [SerializeField] private Button removeButton;
        [SerializeField] private Button deselectButton;

        private PlacementSystem _placement;
        private FarmingController _farming;
        private ProductionController _production;
        private ConstructionController _construction;
        private OrderService _orders;
        private ProgressionService _progression;
        private StorageService _storage;
        private Wallet _wallet;
        private TaskService _tasks;

        public void Initialize(PlacementSystem placement, FarmingController farming, ProductionController production, ConstructionController construction, OrderService orders,
            ProgressionService progression, StorageService storage, Wallet wallet,
            PlayerProgress progress, GameContent content, IAdService ads, Action onMenu)
        {
            _placement = placement;
            _farming = farming;
            _production = production;
            _construction = construction;
            _orders = orders;
            _progression = progression;
            _storage = storage;
            _wallet = wallet;

            topBar.Bind(wallet, progress, storage.Inventory);
            fieldPanel.Initialize(farming, content, wallet, ads, onPlanted: () => _placement.Cancel());
            farming.Notification += toast.Show;
            storage.Notification += toast.Show;
            production.Notification += toast.Show;
            factoryPanel.Initialize(production, content, ads, confirmDialog);
            constructionPanel.Initialize(construction, ads);
            construction.Notification += toast.Show;
            construction.Completed += OnConstructionCompleted;

            ordersPanel.Initialize(orders, content, storage.Inventory);
            ordersButton.onClick.AddListener(OpenOrders);
            orders.Notification += toast.Show;
            progression.LeveledUp += levelUpDialog.Enqueue;

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

        /// <summary>Town tasks (separate from Initialize so the task system stays optional).</summary>
        public void InitializeTasks(TaskService tasks)
        {
            if (tasks == null || tasksPanel == null) return;
            _tasks = tasks;
            tasksPanel.Initialize(tasks);
            tasks.Notification += toast.Show;
        }

        private void OnDestroy()
        {
            if (_tasks != null) _tasks.Notification -= toast.Show;
            if (_placement != null) _placement.StateChanged -= Refresh;
            if (_wallet != null) _wallet.Changed -= Refresh;
            if (_farming != null) _farming.Notification -= toast.Show;
            if (_storage != null) _storage.Notification -= toast.Show;
            if (_production != null) _production.Notification -= toast.Show;
            if (_orders != null) _orders.Notification -= toast.Show;
            if (_construction != null)
            {
                _construction.Notification -= toast.Show;
                _construction.Completed -= OnConstructionCompleted;
            }
            if (_progression != null) _progression.LeveledUp -= levelUpDialog.Enqueue;
        }

        private void OnConstructionCompleted(string instanceId) => Refresh();

        private void OpenOrders()
        {
            if (_placement.CurrentMode == PlacementSystem.Mode.PlacingNew || _placement.CurrentMode == PlacementSystem.Mode.Moving) return;
            _placement.Cancel();
            ordersPanel.Open();
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
            var queue = _production.GetQueue(_placement.TargetInstanceId);
            bool hasProducts = queue != null && !queue.IsEmpty;
            string warning = hasCrop ? "\nThe crop growing on it will be lost."
                : hasProducts ? "\nProducts in its queue (and their ingredients) will be lost."
                : "\nThis cannot be undone and coins are not refunded.";
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

            string target = _placement.TargetInstanceId;
            bool building = selected && target != null && !_construction.IsBuilt(target);
            if (building && constructionPanel.InstanceId != target) constructionPanel.ShowFor(target, definition != null ? definition.DisplayName : "Building");
            else if (!building && constructionPanel.InstanceId != null) constructionPanel.Hide();

            bool factorySelected = selected && !building && definition != null && _production.IsFactory(target, out _);
            if (factorySelected && factoryPanel.FactoryId != _placement.TargetInstanceId) factoryPanel.ShowFor(_placement.TargetInstanceId);
            else if (!factorySelected && factoryPanel.FactoryId != null) factoryPanel.Hide();

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
