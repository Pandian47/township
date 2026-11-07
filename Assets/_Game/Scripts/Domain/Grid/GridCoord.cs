using System;

namespace TownBuilder.Domain
{
    /// <summary>Integer cell coordinate on the isometric grid. Also used for footprint sizes (X = width, Y = depth).</summary>
    public readonly struct GridCoord : IEquatable<GridCoord>
    {
        public readonly int X;
        public readonly int Y;

        public GridCoord(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static GridCoord Zero => new GridCoord(0, 0);
        public static GridCoord One => new GridCoord(1, 1);

        public static GridCoord operator +(GridCoord a, GridCoord b) => new GridCoord(a.X + b.X, a.Y + b.Y);
        public static GridCoord operator -(GridCoord a, GridCoord b) => new GridCoord(a.X - b.X, a.Y - b.Y);
        public static bool operator ==(GridCoord a, GridCoord b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(GridCoord a, GridCoord b) => !(a == b);

        public bool Equals(GridCoord other) => this == other;
        public override bool Equals(object obj) => obj is GridCoord other && this == other;
        public override int GetHashCode() => unchecked((X * 397) ^ Y);
        public override string ToString() => $"({X}, {Y})";
    }
}
