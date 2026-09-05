using System;

namespace Match3Lab.Core
{
    /// <summary>
    /// A board coordinate. X grows to the right, Y grows DOWNWARD: row 0 is the top row,
    /// which is also how rows are written in the level text format. Gravity pulls toward
    /// increasing Y.
    /// </summary>
    public readonly struct GridPos : IEquatable<GridPos>
    {
        public readonly int X;
        public readonly int Y;

        public GridPos(int x, int y)
        {
            X = x;
            Y = y;
        }

        public GridPos Offset(int dx, int dy) => new GridPos(X + dx, Y + dy);

        /// <summary>True when the two positions share an edge (not a corner).</summary>
        public bool IsAdjacentTo(GridPos other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y) == 1;

        public bool Equals(GridPos other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridPos other && Equals(other);
        public override int GetHashCode() => unchecked((X * 397) ^ Y);
        public override string ToString() => "(" + X + "," + Y + ")";

        public static bool operator ==(GridPos a, GridPos b) => a.Equals(b);
        public static bool operator !=(GridPos a, GridPos b) => !a.Equals(b);

        /// <summary>Up, right, down, left — in that order, so iteration is deterministic.</summary>
        public static readonly GridPos[] CardinalOffsets =
        {
            new GridPos(0, -1),
            new GridPos(1, 0),
            new GridPos(0, 1),
            new GridPos(-1, 0),
        };
    }
}
