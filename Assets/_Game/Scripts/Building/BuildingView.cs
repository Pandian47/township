using UnityEngine;
using UnityEngine.Rendering;

namespace TownBuilder
{
    /// <summary>Visual representation of a building (or of the placement preview). Holds no game state of its own.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class BuildingView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;

        private readonly Vector2[] _silhouette = new Vector2[6];

        public string InstanceId { get; private set; }
        public BuildingDefinition Definition { get; private set; }
        public SpriteRenderer SpriteRenderer => spriteRenderer;

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public void Setup(string instanceId, BuildingDefinition definition)
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            InstanceId = instanceId;
            Definition = definition;
            spriteRenderer.sprite = definition.Sprite;
            spriteRenderer.sortingLayerName = SortingLayers.Objects;
            spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
            spriteRenderer.color = Color.white;

            string shortId = string.IsNullOrEmpty(instanceId) ? "?" : instanceId.Substring(0, Mathf.Min(6, instanceId.Length));
            gameObject.name = $"{definition.Id}_{shortId}";
        }

        public void SetAnchor(Vector3 worldAnchor) => transform.position = new Vector3(worldAnchor.x, worldAnchor.y, 0f);

        public void SetTint(Color color) => spriteRenderer.color = color;

        /// <summary>Hit test against the building's isometric silhouette (footprint diamond extruded to the sprite top).</summary>
        public bool HitTest(Vector2 worldPoint, Vector2 axisX, Vector2 axisY)
        {
            if (Definition == null || spriteRenderer.sprite == null) return false;
            var size = Definition.FootprintSize;
            IsoMath.BuildingSilhouette(transform.position, axisX, axisY, size.X, size.Y, spriteRenderer.bounds.max.y, _silhouette);
            return IsoMath.ConvexContains(_silhouette, worldPoint);
        }
    }
}
