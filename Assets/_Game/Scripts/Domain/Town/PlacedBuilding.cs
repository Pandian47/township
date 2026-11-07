namespace TownBuilder.Domain
{
    /// <summary>Runtime state of one building in the player's town. References its definition by stable id only.</summary>
    public sealed class PlacedBuilding
    {
        public PlacedBuilding(string instanceId, string definitionId, GridCoord origin, GridCoord size)
        {
            InstanceId = instanceId;
            DefinitionId = definitionId;
            Origin = origin;
            Size = size;
        }

        public string InstanceId { get; }
        public string DefinitionId { get; }
        public GridCoord Origin { get; internal set; }
        public GridCoord Size { get; }
    }
}
