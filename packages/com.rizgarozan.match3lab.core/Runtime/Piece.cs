using System;

namespace Match3Lab.Core
{
    public enum PieceType : byte
    {
        /// <summary>No piece in the cell (about to be refilled by gravity).</summary>
        Empty = 0,
        /// <summary>An ordinary coloured piece; the only kind that forms matches.</summary>
        Normal,
        /// <summary>Clears its whole row when activated.</summary>
        RocketH,
        /// <summary>Clears its whole column when activated.</summary>
        RocketV,
        /// <summary>Clears the 3x3 area around itself when activated.</summary>
        Bomb,
        /// <summary>Swapped with a piece: clears every piece of that colour.</summary>
        Rainbow,
    }

    /// <summary>What sits in a playable cell. Colour is only meaningful for <see cref="PieceType.Normal"/>.</summary>
    public readonly struct Piece : IEquatable<Piece>
    {
        public readonly PieceType Type;
        public readonly byte Color;

        public Piece(PieceType type, byte color)
        {
            Type = type;
            Color = color;
        }

        public static readonly Piece Empty = new Piece(PieceType.Empty, 0);
        public static Piece Normal(byte color) => new Piece(PieceType.Normal, color);
        public static Piece Special(PieceType type)
        {
            if (type == PieceType.Empty || type == PieceType.Normal)
                throw new ArgumentException("Not a special piece type: " + type, nameof(type));
            return new Piece(type, 0);
        }

        public bool IsEmpty => Type == PieceType.Empty;
        public bool IsNormal => Type == PieceType.Normal;
        public bool IsSpecial => Type >= PieceType.RocketH;
        public bool IsRocket => Type == PieceType.RocketH || Type == PieceType.RocketV;

        public bool Equals(Piece other) => Type == other.Type && Color == other.Color;
        public override bool Equals(object obj) => obj is Piece other && Equals(other);
        public override int GetHashCode() => ((int)Type << 8) | Color;
        public override string ToString() => IsNormal ? "N" + Color : Type.ToString();

        public static bool operator ==(Piece a, Piece b) => a.Equals(b);
        public static bool operator !=(Piece a, Piece b) => !a.Equals(b);
    }
}
