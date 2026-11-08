using System;
using System.Collections.Generic;
using TownBuilder.Domain;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TownBuilder
{
    /// <summary>
    /// Land expansion in the Town scene: darkens land the player doesn't own, puts a "for sale" sign on every plot that
    /// can be bought, opens the land panel when locked ground is tapped, and handles buying (coins + materials).
    /// Rules live in <see cref="LandPlots"/>; prices in <see cref="GameContent"/> (Land).
    /// </summary>
    public sealed class LandController : MonoBehaviour
    {
        [SerializeField] private IsoGrid grid;
        [SerializeField] private PlacementSystem placement;
        [Tooltip("Overlay tilemap that darkens land the player doesn't own.")]
        [SerializeField] private Tilemap lockedTilemap;
        [SerializeField] private TileBase lockedTile;
        [SerializeField] private Sprite saleSign;
        [SerializeField] private Transform signsRoot;

        private readonly List<GameObject> _signs = new List<GameObject>();
        private LandPlots _land;
        private Inventory _inventory;
        private Wallet _wallet;
        private PlayerProgress _progress;
        private GameContent _content;

        public event Action<string> Notification;
        public event Action Modified;
        /// <summary>Locked land was tapped (the plot it belongs to).</summary>
        public event Action<PlotCoord> PlotTapped;
        public event Action<PlotCoord> Bought;
        public event Action Failed;

        public LandPlots Land => _land;

        public void Initialize(LandPlots land, Inventory inventory, Wallet wallet, PlayerProgress progress, GameContent content)
        {
            Unsubscribe();
            _land = land;
            _inventory = inventory;
            _wallet = wallet;
            _progress = progress;
            _content = content;
            _land.Changed += Repaint;
            if (placement != null) placement.GroundTapped += OnGroundTapped;
            Repaint();
        }

        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            if (_land != null) _land.Changed -= Repaint;
            if (placement != null) placement.GroundTapped -= OnGroundTapped;
        }

        // ------------------------------------------------------------------ Queries

        public LandTier TierFor(PlotCoord plot) => _content.LandTierForRing(_land.Ring(plot));

        public bool IsLevelReached(PlotCoord plot)
        {
            var tier = TierFor(plot);
            return tier == null || _progress.IsUnlocked(tier.unlockLevel);
        }

        public bool CanAfford(PlotCoord plot)
        {
            var tier = TierFor(plot);
            if (tier == null) return false;
            if (!_wallet.CanAffordCoins(tier.coins)) return false;
            foreach (var m in tier.materials)
                if (m.material != null && _inventory.Count(m.material.Id) < m.amount) return false;
            return true;
        }

        public int Have(MaterialDefinition material) => material != null ? _inventory.Count(material.Id) : 0;

        public bool HasCoins(int coins) => _wallet.CanAffordCoins(coins);

        // ------------------------------------------------------------------ Commands

        public bool TryBuy(PlotCoord plot)
        {
            var tier = TierFor(plot);
            if (tier == null || !_land.IsForSale(plot)) return false;
            if (!IsLevelReached(plot))
            {
                Notification?.Invoke($"This land opens at level {tier.unlockLevel}.");
                Failed?.Invoke();
                return false;
            }

            if (!CanAfford(plot))
            {
                Notification?.Invoke("Not enough coins or materials for this land yet.");
                Failed?.Invoke();
                return false;
            }

            _wallet.TrySpendCoins(tier.coins);
            foreach (var m in tier.materials)
                if (m.material != null) _inventory.TryRemove(m.material.Id, m.amount);
            _land.Buy(plot);
            Notification?.Invoke("New land! You can build there now.");
            Bought?.Invoke(plot);
            Modified?.Invoke();
            return true;
        }

        // ------------------------------------------------------------------ Visuals

        private void OnGroundTapped(GridCoord cell)
        {
            if (_land == null || !_land.Bounds.Contains(cell) || _land.IsCellUnlocked(cell)) return;
            var plot = _land.PlotOf(cell);
            if (!_land.IsForSale(plot))
            {
                Notification?.Invoke("Buy the land next to it first.");
                return;
            }

            PlotTapped?.Invoke(plot);
        }

        /// <summary>Darkens unowned cells and places a sign on each plot for sale.</summary>
        public void Repaint()
        {
            if (_land == null) return;
            if (lockedTilemap != null)
            {
                lockedTilemap.ClearAllTiles();
                var b = _land.Bounds;
                for (int x = b.MinX; x < b.MaxX; x++)
                for (int y = b.MinY; y < b.MaxY; y++)
                {
                    var cell = new GridCoord(x, y);
                    if (!_land.IsCellUnlocked(cell)) lockedTilemap.SetTile(new Vector3Int(x, y, 0), lockedTile);
                }
            }

            foreach (var s in _signs)
                if (s != null) DestroySafe(s);
            _signs.Clear();
            if (saleSign == null || grid == null) return;

            foreach (var plot in _land.PlotsForSale())
            {
                var origin = _land.PlotOrigin(plot);
                var cells = _land.PlotCells(plot);
                var centre = new GridCoord(origin.X + cells.X / 2, origin.Y + cells.Y / 2);
                var go = new GameObject($"ForSale_{plot.Id}");
                go.transform.SetParent(signsRoot != null ? signsRoot : transform, false);
                var world = grid.CellCenterWorld(centre);
                go.transform.position = new Vector3(world.x, world.y, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = saleSign;
                sr.sortingLayerName = SortingLayers.Objects;
                sr.spriteSortPoint = SpriteSortPoint.Pivot;
                _signs.Add(go);
            }
        }

        public int SignCount => _signs.Count;

        /// <summary>Editor previews: draws the overlay and signs for <paramref name="land"/> without any gameplay hooks.</summary>
        public void PreviewPaint(LandPlots land)
        {
            _land = land;
            Repaint();
        }

        private static void DestroySafe(GameObject go)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
    }
}
