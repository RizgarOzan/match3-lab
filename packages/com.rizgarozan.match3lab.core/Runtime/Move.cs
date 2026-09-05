using System;

namespace Match3Lab.Core
{
    public enum MoveKind : byte
    {
        /// <summary>Swap two edge-adjacent pieces.</summary>
        Swap,
        /// <summary>Activate a special piece in place.</summary>
        Tap,
    }

    /// <summary>A player (or bot) action. For a swap, <see cref="A"/> is where the drag started and
    /// <see cref="B"/> where the piece was dropped; the moved piece ends up at B.</summary>
    public readonly struct Move : IEquatable<Move>
    {
        public readonly MoveKind Kind;
        public readonly GridPos A;
        public readonly GridPos B;

        private Move(MoveKind kind, GridPos a, GridPos b)
        {
            Kind = kind;
            A = a;
            B = b;
        }

        public static Move Swap(GridPos a, GridPos b) => new Move(MoveKind.Swap, a, b);
        public static Move Swap(int ax, int ay, int bx, int by) => new Move(MoveKind.Swap, new GridPos(ax, ay), new GridPos(bx, by));
        public static Move Tap(GridPos p) => new Move(MoveKind.Tap, p, p);
        public static Move Tap(int x, int y) => Tap(new GridPos(x, y));

        public bool Equals(Move other) => Kind == other.Kind && A == other.A && B == other.B;
        public override bool Equals(object obj) => obj is Move other && Equals(other);
        public override int GetHashCode() => unchecked((((int)Kind * 397) ^ A.GetHashCode()) * 397 ^ B.GetHashCode());
        public override string ToString() => Kind == MoveKind.Tap ? "tap " + A : A + "<->" + B;
    }
}
