namespace TownBuilder.Domain
{
    /// <summary>Axis-aligned rectangle of grid cells. Min is inclusive, Max is exclusive.</summary>
    public readonly struct GridRect
    {
        public readonly int MinX;
        public readonly int MinY;
        public readonly int Width;
        public readonly int Height;

        public GridRect(int minX, int minY, int width, int height)
        {
            MinX = minX;
            MinY = minY;
            Width = width < 0 ? 0 : width;
            Height = height < 0 ? 0 : height;
        }

        public int MaxX => MinX + Width;
        public int MaxY => MinY + Height;
        public bool IsEmpty => Width == 0 || Height == 0;

        public bool Contains(GridCoord cell) =>
            cell.X >= MinX && cell.X < MaxX && cell.Y >= MinY && cell.Y < MaxY;

        public bool ContainsArea(GridCoord origin, GridCoord size) =>
            size.X > 0 && size.Y > 0 &&
            origin.X >= MinX && origin.Y >= MinY &&
            origin.X + size.X <= MaxX && origin.Y + size.Y <= MaxY;

        /// <summary>Clamps a footprint origin so the whole footprint stays inside the rectangle (when it fits).</summary>
        public GridCoord ClampOrigin(GridCoord origin, GridCoord size)
        {
            int maxX = MaxX - size.X;
            int maxY = MaxY - size.Y;
            int x = origin.X > maxX ? maxX : origin.X;
            int y = origin.Y > maxY ? maxY : origin.Y;
            if (x < MinX) x = MinX;
            if (y < MinY) y = MinY;
            return new GridCoord(x, y);
        }

        public override string ToString() => $"[{MinX},{MinY} {Width}x{Height}]";
    }
}
