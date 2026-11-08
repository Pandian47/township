using System.Collections.Generic;
using TownBuilder.Domain;
using UnityEngine;

namespace TownBuilder
{
    /// <summary>
    /// The one place that turns Town events into visual feedback: building pops (place, move, harvest, collect,
    /// construction finished), the selection outline, and floating reward labels. Like TownAudioBridge for sound,
    /// gameplay code only raises events and never plays effects itself.
    /// </summary>
    public sealed class TownEffects : MonoBehaviour
    {
        [SerializeField] private TownBuildingController buildings;
        [SerializeField] private PlacementSystem placement;
        [SerializeField] private FarmingController farming;
        [SerializeField] private ProductionController production;
        [SerializeField] private ConstructionController construction;
        [SerializeField] private SelectionOutline selectionOutline;
        [SerializeField] private FloatingTextLayer floatingText;

        [Header("Look")]
        [SerializeField] private Color itemTextColor = Color.white;
        [SerializeField] private Color xpTextColor = new Color(0.6f, 0.88f, 1f);
        [SerializeField] private Color coinsTextColor = new Color(1f, 0.85f, 0.3f);
        [SerializeField] private float placePopStrength = 0.14f;
        [SerializeField] private float rewardPopStrength = 0.1f;
        [Tooltip("Label start height above the top of the building (world units).")]
        [SerializeField] private float labelOffset = 0.15f;

        private Town _town;

        public SelectionOutline Outline => selectionOutline;
        public FloatingTextLayer FloatingText => floatingText;

        public void Initialize(Town town)
        {
            Unsubscribe();
            _town = town;
            _town.BuildingMoved += OnBuildingMoved;
            placement.BuildingPlaced += OnBuildingPlaced;
            placement.StateChanged += OnPlacementStateChanged;
            farming.Rewarded += OnRewarded;
            production.Rewarded += OnRewarded;
            production.SlotAdded += OnSlotAdded;
            construction.Rewarded += OnRewarded;
            OnPlacementStateChanged();
        }

        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            if (_town == null) return;
            _town.BuildingMoved -= OnBuildingMoved;
            if (placement != null)
            {
                placement.BuildingPlaced -= OnBuildingPlaced;
                placement.StateChanged -= OnPlacementStateChanged;
            }

            if (farming != null) farming.Rewarded -= OnRewarded;
            if (production != null)
            {
                production.Rewarded -= OnRewarded;
                production.SlotAdded -= OnSlotAdded;
            }
            if (construction != null) construction.Rewarded -= OnRewarded;
            _town = null;
        }

        // ------------------------------------------------------------------ Public helpers

        /// <summary>Pops a building's view (no-op if it has none).</summary>
        public void Pop(string instanceId, float strength)
        {
            if (buildings.TryGetView(instanceId, out var view)) PopAnimator.Play(view.transform, PopAnimator.Style.Squash, strength);
        }

        /// <summary>Shows reward labels above a building.</summary>
        public void ShowReward(WorldReward reward)
        {
            if (reward == null || floatingText == null || !buildings.TryGetView(reward.BuildingId, out var view)) return;

            var lines = new List<FloatingLine>();
            foreach (var gain in reward.Items)
            {
                if (gain.Item == null || gain.Amount <= 0) continue;
                lines.Add(new FloatingLine($"+{gain.Amount} {gain.Item.DisplayName}", itemTextColor, gain.Item.Icon));
            }

            if (reward.Coins > 0) lines.Add(new FloatingLine($"+{reward.Coins:N0} coins", coinsTextColor));
            if (reward.Xp > 0) lines.Add(new FloatingLine($"+{reward.Xp} XP", xpTextColor));
            if (lines.Count == 0) return;

            var bounds = view.SpriteRenderer.bounds;
            floatingText.Spawn(new Vector3(bounds.center.x, bounds.max.y + labelOffset, 0f), lines);
        }

        // ------------------------------------------------------------------ Events

        private void OnBuildingPlaced(PlacedBuilding building) => Pop(building.InstanceId, placePopStrength);

        private void OnBuildingMoved(PlacedBuilding building) => Pop(building.InstanceId, placePopStrength);

        private void OnSlotAdded(string factoryId) => Pop(factoryId, placePopStrength);

        private void OnRewarded(WorldReward reward)
        {
            Pop(reward.BuildingId, rewardPopStrength);
            ShowReward(reward);
        }

        private void OnPlacementStateChanged()
        {
            if (selectionOutline == null) return;
            if (placement.CurrentMode == PlacementSystem.Mode.Selected &&
                buildings.TryGetView(placement.TargetInstanceId, out var view))
                selectionOutline.Show(view);
            else
                selectionOutline.Hide();
        }
    }
}
