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
    }
}
