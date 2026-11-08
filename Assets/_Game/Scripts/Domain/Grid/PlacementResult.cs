namespace TownBuilder.Domain
{
    public enum PlacementResult
    {
        Valid,
        InvalidSize,
        OutOfBounds,
        BlockedTerrain,
        Overlap,
        DuplicateId,
        NotFound,
        /// <summary>The land isn't owned yet (land expansion).</summary>
        LockedLand,
    }
}
