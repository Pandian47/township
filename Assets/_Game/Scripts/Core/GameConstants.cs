namespace TownBuilder
{
    public static class SceneNames
    {
        public const string Boot = "Boot";
        public const string MainMenu = "MainMenu";
        public const string Town = "Town";
    }

    /// <summary>
    /// Sorting layers (back to front). Everything that needs depth sorting against other objects
    /// (buildings, crops, trees, characters, vehicles) MUST live on <see cref="Objects"/>; the 2D renderer
    /// sorts that layer by the Y of each sprite's pivot (Transparency Sort Axis = 0,1,0).
    /// </summary>
    public static class SortingLayers
    {
        public const string Background = "Background";
        public const string Ground = "Ground";
        public const string GroundOverlay = "GroundOverlay";
        public const string Objects = "Objects";
        public const string Effects = "Effects";
        public const string WorldUI = "WorldUI";
    }

    public static class ArtConventions
    {
        /// <summary>Pixels per unit for every world sprite. A 128x64 px ground tile = 1 x 0.5 world units = one grid cell.</summary>
        public const float PixelsPerUnit = 128f;
    }
}
