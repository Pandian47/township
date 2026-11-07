using System.Collections.Generic;
using TownBuilder.Domain;
using UnityEngine;

namespace TownBuilder
{
    /// <summary>Keeps one <see cref="BuildingView"/> per building in the domain <see cref="Town"/>, reacting to its events.</summary>
    public sealed class TownBuildingController : MonoBehaviour
    {
        public const string PreviewInstanceId = "preview";

        [SerializeField] private IsoGrid grid;
        [SerializeField] private BuildingView buildingPrefab;
        [SerializeField] private Transform buildingsRoot;

        private readonly Dictionary<string, BuildingView> _views = new Dictionary<string, BuildingView>();
        private Town _town;
        private GameContent _content;

        public void Initialize(Town town, GameContent content)
        {
            Unsubscribe();
            _town = town;
            _content = content;
            _town.BuildingAdded += OnBuildingAdded;
            _town.BuildingMoved += OnBuildingMoved;
            _town.BuildingRemoved += OnBuildingRemoved;
            foreach (var b in _town.Buildings) OnBuildingAdded(b);
        }

        public bool TryGetView(string instanceId, out BuildingView view) => _views.TryGetValue(instanceId, out view);

        public BuildingView CreatePreview(BuildingDefinition definition)
        {
            var view = Instantiate(buildingPrefab, buildingsRoot);
            view.Setup(PreviewInstanceId, definition);
            return view;
        }

        /// <summary>Finds the front-most building whose silhouette contains the world point.</summary>
        public bool TryPick(Vector2 worldPoint, out PlacedBuilding building)
        {
            building = null;
            BuildingView best = null;
            var axisX = grid.AxisX;
            var axisY = grid.AxisY;
            foreach (var view in _views.Values)
            {
                if (!view.HitTest(worldPoint, axisX, axisY)) continue;
                // Lower anchor = closer to the camera = drawn in front.
                if (best == null || view.transform.position.y < best.transform.position.y) best = view;
            }

            if (best != null) return _town.TryGet(best.InstanceId, out building);
            return _town.TryGetAt(grid.WorldToCell(worldPoint), out building);
        }

        private void OnBuildingAdded(PlacedBuilding b)
        {
            if (!_content.TryGetBuilding(b.DefinitionId, out var definition))
            {
                Debug.LogWarning($"[Town] No definition for '{b.DefinitionId}', building has no visual.");
                return;
            }

            var view = Instantiate(buildingPrefab, buildingsRoot);
            view.Setup(b.InstanceId, definition);
            view.SetAnchor(grid.FootprintAnchorWorld(b.Origin));
            _views[b.InstanceId] = view;
        }

        private void OnBuildingMoved(PlacedBuilding b)
        {
            if (_views.TryGetValue(b.InstanceId, out var view)) view.SetAnchor(grid.FootprintAnchorWorld(b.Origin));
        }

        private void OnBuildingRemoved(PlacedBuilding b)
        {
            if (!_views.TryGetValue(b.InstanceId, out var view)) return;
            _views.Remove(b.InstanceId);
            Destroy(view.gameObject);
        }

        private void Unsubscribe()
        {
            if (_town == null) return;
            _town.BuildingAdded -= OnBuildingAdded;
            _town.BuildingMoved -= OnBuildingMoved;
            _town.BuildingRemoved -= OnBuildingRemoved;
        }

        private void OnDestroy() => Unsubscribe();
    }
}
