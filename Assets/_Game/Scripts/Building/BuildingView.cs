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

        /// <summary>Shown in its rotated facing (v0.40.0).</summary>
        public bool Flipped { get; private set; }

        /// <summary>True when the rotated facing is the normal picture mirrored (no own right-facing picture).</summary>
        public bool Mirrored => Flipped && (Definition == null || Definition.FlippedSprite == null);

        /// <summary>
        /// The picture for the current facing (frame 0 when animated), fitted to the footprint when it is much too
        /// large (<see cref="SpriteFit"/>, v0.40.2).
        /// </summary>
        public Sprite FacingSprite => Definition == null ? null
            : Fitted(Flipped && Definition.FlippedSprite != null ? Definition.FlippedSprite : Definition.Sprite);

        /// <summary>Any picture of this building (seasonal ones too) as it should be drawn on its plot.</summary>
        public Sprite Fitted(Sprite sprite) => Definition == null ? sprite : SpriteFit.ForFootprint(sprite, Definition.FootprintSize);

        /// <summary>
        /// Where a point measured 0-1 on the NORMAL picture (from its bottom-left) is now, relative to the building's anchor
        /// (unscaled): effects such as chimney smoke, grain and Z letters use it, so they follow the rotated facing too.
        /// A right-facing picture is assumed to be roughly the mirror image of the normal one.
        /// </summary>
        public Vector3 PointLocal(Vector2 point)
        {
            var sprite = spriteRenderer.sprite;
            if (sprite == null) return Vector3.zero;
            float ppu = sprite.pixelsPerUnit;
            var rect = sprite.rect;
            float y = (point.y * rect.height - sprite.pivot.y) / ppu;
            if (Mirrored) return new Vector3(-(point.x * rect.width - sprite.pivot.x) / ppu, y, 0f);
            float px = Flipped ? 1f - point.x : point.x;
            return new Vector3((px * rect.width - sprite.pivot.x) / ppu, y, 0f);
        }

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public void Setup(string instanceId, BuildingDefinition definition)
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            InstanceId = instanceId;
            Definition = definition;
            Flipped = false;
            spriteRenderer.flipX = false;
            spriteRenderer.sprite = FacingSprite;
            spriteRenderer.sortingLayerName = SortingLayers.Objects;
            spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
            spriteRenderer.color = Color.white;

            string shortId = string.IsNullOrEmpty(instanceId) ? "?" : instanceId.Substring(0, Mathf.Min(6, instanceId.Length));
            gameObject.name = $"{definition.Id}_{shortId}";
        }

        public void SetAnchor(Vector3 worldAnchor) => transform.position = new Vector3(worldAnchor.x, worldAnchor.y, 0f);

        /// <summary>
        /// Shows the normal or rotated facing: the building's own right-facing picture if it has one, otherwise the
        /// normal picture mirrored around its pivot (the footprint's bottom corner, so it stays on its cells).
        /// </summary>
        public void SetFacing(bool flipped)
        {
            Flipped = flipped;
            spriteRenderer.sprite = FacingSprite;
            spriteRenderer.flipX = Mirrored;
        }

        public void SetTint(Color color) => spriteRenderer.color = color;

        /// <summary>Hit test against the building's isometric silhouette (footprint diamond extruded to the sprite top).</summary>
        public bool HitTest(Vector2 worldPoint, Vector2 axisX, Vector2 axisY)
        {
            if (Definition == null || spriteRenderer.sprite == null) return false;
            var size = Domain.PlacedBuilding.SizeFor(Definition.FootprintSize, Flipped);
            IsoMath.BuildingSilhouette(transform.position, axisX, axisY, size.X, size.Y, spriteRenderer.bounds.max.y, _silhouette);
            return IsoMath.ConvexContains(_silhouette, worldPoint);
        }
    }
}
