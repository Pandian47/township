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
        [SerializeField] private TrainPanel trainPanel;
        [SerializeField] private TruckPanel truckPanel;
        [SerializeField] private HomePanel homePanel;
        [SerializeField] private LandPanel landPanel;
        [SerializeField] private DailyRewardDialog dailyDialog;
        [SerializeField] private AchievementsPanel achievementsPanel;
        [SerializeField] private EventPanel eventPanel;
        [SerializeField] private CollectionsPanel collectionsPanel;
        [SerializeField] private LandscapePanel landscapePanel;
        [SerializeField] private DevPanel devPanel;
        [SerializeField] private RewardFlyer rewardFlyer;

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
        [Tooltip("The round menu button opens Settings (sound, credits, privacy, main menu).")]
        [SerializeField] private Button menuButton;
        [SerializeField] private SettingsPanel settingsPanel;
        [Tooltip("Phone back button: closes the top window, or asks to quit.")]
        [SerializeField] private BackButtonHandler backButton;

        [Header("Placement bar (placing / moving)")]
        [SerializeField] private GameObject placementBar;
        [SerializeField] private TMP_Text placementInfoText;
        [SerializeField] private Button confirmPlacementButton;
        [SerializeField] private Button cancelPlacementButton;
        [SerializeField] private Button rotatePlacementButton;

        [Header("Selection bar")]
        [SerializeField] private GameObject selectionBar;
        [SerializeField] private TMP_Text selectionNameText;
        [SerializeField] private Button moveButton;
        [Tooltip("Turns the selected building to its other facing (v0.40.0).")]
        [SerializeField] private Button rotateButton;
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
        private TrainController _train;
        private TruckController _truck;
        private HappinessController _homes;
        private LandscapeController _landscape;
        private LandController _land;

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
            if (rewardFlyer != null) rewardFlyer.Initialize(wallet, progress);
            fieldPanel.Initialize(farming, content, wallet, ads, onPlanted: () => _placement.Cancel());
            farming.Notification += toast.Show;
            storage.Notification += toast.Show;
            production.Notification += toast.Show;
            factoryPanel.Initialize(production, content, ads, confirmDialog);
            factoryPanel.CloseRequested += () => _placement.Cancel();
            factoryPanel.NavigateRequested += id => _placement.SelectBuilding(id);
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

            // Ads off (AdsConfig > Ads Enabled, or no ad SDK on this platform) = NoAdService: the button is hidden.
            bool adsOn = content.AdsConfig != null && !(ads is NoAdService);
            rewardedCoinsButton.gameObject.SetActive(adsOn);
            if (adsOn) rewardedCoinsButton.Initialize(ads, wallet, content.AdsConfig.RewardedCoins);
            shopPanel.Initialize(content, wallet, progress, _placement.CanBuildMore, d =>
            {
                if (d.IsLandscapeTool) _landscape?.Begin(d.LandscapeTool);
                else _placement.BeginPlacement(d);
            }, _placement.CountOwned);

            buildButton.onClick.AddListener(() =>
            {
                _placement.Cancel();
                shopPanel.Open();
            });
            if (settingsPanel != null)
            {
                settingsPanel.Initialize(ads, content.AdsConfig != null ? content.AdsConfig.PrivacyPolicyUrl : "", onMenu);
                menuButton.onClick.AddListener(() =>
                {
                    _placement.Cancel();
                    settingsPanel.Open();
                });
            }
            else
            {
                menuButton.onClick.AddListener(() => onMenu?.Invoke());
            }

            confirmPlacementButton.onClick.AddListener(() => _placement.Confirm());
            cancelPlacementButton.onClick.AddListener(() => _placement.Cancel());

            moveButton.onClick.AddListener(() => _placement.BeginMoveSelected());
            if (rotateButton != null) rotateButton.onClick.AddListener(RotateSelected);
            if (rotatePlacementButton != null) rotatePlacementButton.onClick.AddListener(() => _placement.Rotate());
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
            // Any other button (Build, Barn, Orders, menu, chest, event...) closes the task list, so it never stays
            // open behind a window.
            foreach (var button in GetComponentsInChildren<Button>(true))
                if (!button.transform.IsChildOf(tasksPanel.transform))
                    button.onClick.AddListener(tasksPanel.Close);
        }

        /// <summary>Truck Depot panel (shown while the finished depot is selected).</summary>
        public void InitializeHomes(HappinessController homes)
        {
            if (homes == null || homePanel == null) return;
            _homes = homes;
            homePanel.Initialize(homes);
            Refresh();
        }

        public void InitializeTruck(TruckController truck, GameContent content, Inventory inventory, IAdService ads)
        {
            if (truck == null || truckPanel == null) return;
            _truck = truck;
            truckPanel.Initialize(truck, content, inventory, ads);
            truck.Notification += toast.Show;
            Refresh();
        }

        /// <summary>Train Station panel (shown while the finished station is selected).</summary>
        public void InitializeTrain(TrainController train, GameContent content, Inventory inventory, IAdService ads)
        {
            if (train == null || trainPanel == null) return;
            _train = train;
            trainPanel.Initialize(train, content, inventory, ads);
            train.Notification += toast.Show;
            Refresh();
        }

        /// <summary>Land expansion: tapping locked land opens the land panel.</summary>
        /// <summary>Developer-mode shortcuts (Editor and test APKs only; hidden otherwise).</summary>
        public void InitializeDev(TownSceneController scene, GameContent content)
        {
            if (devPanel != null) devPanel.Initialize(scene, content, toast.Show);
        }

        /// <summary>Daily reward calendar (opens by itself when a reward is waiting).</summary>
        public void InitializeDaily(DailyRewardService daily)
        {
            if (daily == null || dailyDialog == null) return;
            dailyDialog.Initialize(daily);
            daily.Notification += toast.Show;
        }

        public DailyRewardDialog DailyDialog => dailyDialog;

        /// <summary>Achievements: the Goals button and screen.</summary>
        public void InitializeAchievements(AchievementService achievements)
        {
            if (achievements == null || achievementsPanel == null) return;
            achievementsPanel.Initialize(achievements);
            achievements.Notification += toast.Show;
        }

        public AchievementsPanel AchievementsPanel => achievementsPanel;

        /// <summary>Weekend events: the event button + window, and event-only decorations in the shop.</summary>
        /// <summary>Landscape painting: the paint bar (the Landscape card in Build → Decorations starts it).</summary>
        public void InitializeLandscape(LandscapeController landscape)
        {
            if (landscape == null) return;
            _landscape = landscape;
            if (landscapePanel != null) landscapePanel.Initialize(landscape);
            landscape.Notification += toast.Show;
        }

        public LandscapePanel LandscapePanel => landscapePanel;
        public SettingsPanel SettingsPanel => settingsPanel;
        public BackButtonHandler BackButton => backButton;
        public TasksPanel TasksPanel => tasksPanel;
        public Button MenuButton => menuButton;

        /// <summary>Collections: the chest button and window.</summary>
        public void InitializeCollections(CollectionService collections)
        {
            if (collections == null || collectionsPanel == null) return;
            collectionsPanel.Initialize(collections);
            collections.Notification += toast.Show;
        }

        public CollectionsPanel CollectionsPanel => collectionsPanel;

        /// <summary>Seasons: season toasts and seasonal decorations in the shop.</summary>
        public void InitializeSeasons(SeasonController seasons)
        {
            if (seasons == null) return;
            if (shopPanel != null) shopPanel.AddListingFilter(seasons.IsListedInShop);
            seasons.Notification += toast.Show;
        }

        public void InitializeEvents(WeekendEventService events)
        {
            if (events == null) return;
            if (eventPanel != null) eventPanel.Initialize(events);
            if (shopPanel != null) shopPanel.AddListingFilter(events.IsListedInShop);
            events.Notification += toast.Show;
        }

        public EventPanel EventPanel => eventPanel;

        public void InitializeLand(LandController land)
        {
            if (land == null || landPanel == null) return;
            _land = land;
            landPanel.Initialize(land);
            land.PlotTapped += landPanel.ShowFor;
            land.Notification += toast.Show;
        }

        private void OnDestroy()
        {
            if (_land != null)
            {
                _land.PlotTapped -= landPanel.ShowFor;
                _land.Notification -= toast.Show;
            }

            if (_tasks != null) _tasks.Notification -= toast.Show;
            if (_train != null) _train.Notification -= toast.Show;
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

        /// <summary>Turns the selected building where it stands; explains when the turned footprint doesn't fit.</summary>
        private void RotateSelected()
        {
            var result = _placement.Rotate();
            if (result == PlacementResult.Valid || result == PlacementResult.NotFound) return;
            toast.Show("No room to turn it here. Move it to a free spot, then rotate.");
        }

        private void Refresh()
        {
            var mode = _placement.CurrentMode;
            bool placing = mode == PlacementSystem.Mode.PlacingNew || mode == PlacementSystem.Mode.Moving;
            bool selected = mode == PlacementSystem.Mode.Selected;

            bottomNavigation.SetActive(!placing && !selected); // also hides the rewarded-ad button inside it
            if ((placing || selected) && landPanel != null && landPanel.IsOpen) landPanel.Hide();
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
                moveButton.interactable = definition.Movable;
                if (rotateButton != null) rotateButton.interactable = definition.Movable;
            }

            string target = _placement.TargetInstanceId;
            bool building = selected && target != null && !_construction.IsBuilt(target);
            if (building && constructionPanel.InstanceId != target) constructionPanel.ShowFor(target, definition != null ? definition.DisplayName : "Building");
            else if (!building && constructionPanel.InstanceId != null) constructionPanel.Hide();

            bool factorySelected = selected && !building && definition != null && _production.IsFactory(target, out _);
            if (factorySelected && factoryPanel.FactoryId != _placement.TargetInstanceId) factoryPanel.ShowFor(_placement.TargetInstanceId);
            else if (!factorySelected && factoryPanel.FactoryId != null) factoryPanel.Hide();

            bool stationSelected = selected && !building && _train != null && _train.IsStation(target);
            if (stationSelected) trainPanel.Show();
            else if (trainPanel != null && trainPanel.IsOpen) trainPanel.Hide();

            bool homeSelected = selected && !building && _homes != null && _homes.IsHome(target, out _);
            if (homeSelected && homePanel.HomeId != target) homePanel.ShowFor(target);
            else if (!homeSelected && homePanel != null && homePanel.HomeId != null) homePanel.Hide();

            bool depotSelected = selected && !building && _truck != null && _truck.IsDepot(target);
            if (depotSelected) truckPanel.Show();
            else if (truckPanel != null && truckPanel.IsOpen) truckPanel.Hide();

            bool fieldSelected = selected && !building && definition != null && (definition.IsFarmPlot || definition.IsFruitTree);
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
                PlacementResult.LockedLand => "Buy this land first",
                _ => _placement.PreviewValidity.ToString(),
            };

            if (mode == PlacementSystem.Mode.PlacingNew)
            {
                string cost = _placement.CanAffordActive ? $"{definition.CoinCost:N0} coins" : $"Need {definition.CoinCost:N0} coins";
                string happy = definition.Happiness > 0 ? $"  -  homes nearby +{definition.Happiness} happiness" : "";
                return $"{definition.DisplayName}  -  {cost}{happy}\n{state}";
            }

            return $"Moving {definition.DisplayName}\n{state}";
        }
    }
}
