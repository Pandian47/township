using TownBuilder.Domain;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TownBuilder
{
    /// <summary>
    /// A road tile that joins up with the roads next to it (v0.36.0): 16 pictures, one per combination of neighbours,
    /// with a kerb on every side that doesn't touch another road (see <see cref="RoadJoins"/> for the bit order).
    /// Any road style counts as a neighbour, so different styles meet without a kerb between them.
    /// The pictures are drawn by the editor tool RoadTileBuilder from the style's flat tile.
    /// </summary>
    [CreateAssetMenu(menuName = "TownBuilder/Joined Road Tile", fileName = "JoinedRoadTile")]
    public sealed class JoinedRoadTile : TileBase
    {
        [Tooltip("16 pictures, index = neighbour mask (+X=1, +Y=2, -X=4, -Y=8).")]
        [SerializeField] private Sprite[] sprites = new Sprite[16];

        /// <summary>The picture with no neighbours (kerbs all round): used for swatches.</summary>
        public Sprite Preview => SpriteFor(0);

        public Sprite SpriteFor(int mask) => sprites != null && mask >= 0 && mask < sprites.Length ? sprites[mask] : null;

        public static int MaskAt(ITilemap tilemap, Vector3Int position) =>
            RoadJoins.Mask(new GridCoord(position.x, position.y), c => tilemap.GetTile(new Vector3Int(c.X, c.Y, position.z)) is JoinedRoadTile);

        public override void RefreshTile(Vector3Int position, ITilemap tilemap)
        {
            tilemap.RefreshTile(position);
            foreach (var offset in new[] { Vector3Int.right, Vector3Int.up, Vector3Int.left, Vector3Int.down })
            {
                var neighbour = position + offset;
                if (tilemap.GetTile(neighbour) is JoinedRoadTile) tilemap.RefreshTile(neighbour);
            }
        }

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            tileData.sprite = SpriteFor(MaskAt(tilemap, position)) ?? SpriteFor(0);
            tileData.color = Color.white;
            tileData.flags = TileFlags.LockTransform;
            tileData.colliderType = Tile.ColliderType.None;
        }

#if UNITY_EDITOR
        public void EditorSetSprites(Sprite[] value) => sprites = value;
#endif
    }
}
