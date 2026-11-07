using System;
using System.Collections.Generic;
using TownBuilder.Domain;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TownBuilder
{
    /// <summary>
    /// Composition root of the Town scene: builds the domain model from the active save, hands it to the
    /// scene systems, and saves (debounced autosave + on pause/quit/leave).
    /// Works when the scene is opened directly in the Editor (creates GameRoot and loads/creates a save).
    /// </summary>
    public sealed class TownSceneController : MonoBehaviour
    {
        [Tooltip("Used only when the scene is played directly without going through Boot.")]
        [SerializeField] private GameContent fallbackContent;
        [SerializeField] private IsoGrid grid;
        [SerializeField] private IsoCameraController cameraController;
        [SerializeField] private TownBuildingController buildings;
        [SerializeField] private PlacementSystem placement;
        [SerializeField] private FarmingController farming;
        [SerializeField] private TownHudController hud;
        [SerializeField] private float startOrthographicSize = 6f;
        [SerializeField] private float autosaveDelaySeconds = 2f;

        private GameContext _context;
        private Town _town;
        private Wallet _wallet;
        private PlayerProgress _progress;
        private FarmPlots _plots;
        private Inventory _inventory;
        private StorageService _storage;
        private float _saveDueAt = -1f;

        public Town Town => _town;
        public Wallet Wallet => _wallet;
        public PlayerProgress Progress => _progress;
        public PlacementSystem Placement => placement;
        public FarmingController Farming => farming;
        public Inventory Inventory => _inventory;
        public StorageService Storage => _storage;
        public bool IsReady => _town != null;

        private void Start()
        {
            var root = GameRoot.GetOrCreate(fallbackContent);
            if (root == null) { enabled = false; return; }
            _context = root.Context;

            if (!_context.HasActiveSave)
            {
                var load = _context.ContinueGame();
                if (!load.Succeeded)
                {
                    if (load.Status == LoadStatus.FromNewerVersion)
                    {
                        Debug.LogError("[Town] Save file is from a newer game version; refusing to overwrite it.");
                        enabled = false;
                        return;
                    }

                    _context.StartNewGame();
                }
            }

            var save = _context.ActiveSave;
            var warnings = new List<string>();

            grid.Build();
            _town = new Town(grid.Bounds, grid.BlockedCells);
            _wallet = TownSaveMapper.CreateWallet(save);
            _progress = TownSaveMapper.CreateProgress(save);

            buildings.Initialize(_town, _context.Content);
            TownSaveMapper.LoadBuildings(save, _town, _context.Content, warnings);

            _plots = new FarmPlots();
            _inventory = new Inventory(_context.Content.BaseStorageCapacity);
            TownSaveMapper.LoadFarming(save, _town, _context.Content, _plots, _inventory, warnings);
            foreach (var w in warnings) Debug.LogWarning($"[Town] {w}");

            var content = _context.Content;
            var upgrades = new StorageUpgrades(save.player.storageLevel, content.StorageExpansionCapacity,
                content.StorageExpansionBaseCost, content.StorageExpansionMaxLevel);
            _storage = new StorageService(_town, _inventory, _wallet, upgrades, content);
            _storage.Modified += ScheduleSave;

            placement.Initialize(_town, _wallet, _progress, _context.Content);
            placement.TownModified += ScheduleSave;

            farming.Initialize(_town, _plots, _inventory, _wallet, _progress, _context.Content, _context.Clock);
            farming.Modified += ScheduleSave;

            cameraController.SetWorldBounds(grid.WorldBounds());
            cameraController.SnapTo(grid.WorldBounds().center, startOrthographicSize);

            _wallet.Changed += ScheduleSave; // e.g. coins from a rewarded ad

            hud.Initialize(placement, farming, _storage, _wallet, _progress, _context.Content, _context.Ads, ReturnToMainMenu);
        }

        private void OnDestroy()
        {
            if (_storage != null)
            {
                _storage.Modified -= ScheduleSave;
                _storage.Dispose();
            }

            if (placement != null) placement.TownModified -= ScheduleSave;
            if (farming != null) farming.Modified -= ScheduleSave;
            if (_wallet != null) _wallet.Changed -= ScheduleSave;
        }

        private void Update()
        {
            if (_saveDueAt >= 0f && Time.unscaledTime >= _saveDueAt) SaveNow();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) SaveNow();
        }

        private void OnApplicationQuit() => SaveNow();

        public void SaveNow()
        {
            _saveDueAt = -1f;
            if (_town == null || _context?.ActiveSave == null) return;

            try
            {
                TownSaveMapper.WriteTo(_context.ActiveSave, _town, _wallet, _progress, _plots, _inventory);
                _context.ActiveSave.player.storageLevel = _storage.Upgrades.Level;
                _context.SaveActive();
            }
            catch (Exception e)
            {
                // Never crash the game because of a failed save; the next autosave retries.
                Debug.LogException(e);
            }
        }

        private void ScheduleSave() => _saveDueAt = Time.unscaledTime + autosaveDelaySeconds;

        private void ReturnToMainMenu()
        {
            SaveNow();
            // Natural break point: an interstitial may show here (respects the cooldown in AdsConfig).
            _context.Ads.ShowInterstitialIfAllowed(() => SceneManager.LoadScene(SceneNames.MainMenu));
        }
    }
}
