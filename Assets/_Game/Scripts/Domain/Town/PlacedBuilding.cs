namespace TownBuilder.Domain
{
    /// <summary>Runtime state of one building in the player's town. References its definition by stable id only.</summary>
    public sealed class PlacedBuilding
    {
        public PlacedBuilding(string instanceId, string definitionId, GridCoord origin, GridCoord size, bool flipped = false)
        {
            InstanceId = instanceId;
            DefinitionId = definitionId;
            Origin = origin;
            Size = size;
            Flipped = flipped;
        }

        public string InstanceId { get; }
        public string DefinitionId { get; }
        public GridCoord Origin { get; internal set; }

        /// <summary>The footprint as placed (already swapped when <see cref="Flipped"/>).</summary>
        public GridCoord Size { get; internal set; }

        /// <summary>
        /// Rotated to its other facing (v0.40.0): the picture is mirrored (or its own right-facing picture is used) and the
        /// footprint's X and Y swap.
        /// </summary>
        public bool Flipped { get; internal set; }

        /// <summary>The footprint for a facing: the definition's size, with X and Y swapped when flipped.</summary>
        public static GridCoord SizeFor(GridCoord baseSize, bool flipped) => flipped ? new GridCoord(baseSize.Y, baseSize.X) : baseSize;
    }
}
