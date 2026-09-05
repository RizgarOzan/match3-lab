namespace Match3Lab.Core
{
    public enum BoardEventKind : byte
    {
        /// <summary>Pieces at Pos and To changed places.</summary>
        Swap,
        /// <summary>A normal piece at Pos was removed (Piece says which).</summary>
        Cleared,
        /// <summary>One layer of the obstacle in Layer was removed at Pos.</summary>
        ObstacleHit,
        /// <summary>A box at Pos lost its last hit point; the cell is playable from now on.</summary>
        BoxDestroyed,
        /// <summary>A special piece (Piece) appeared at Pos as the reward for a match.</summary>
        SpecialCreated,
        /// <summary>The special piece at Pos fired and was consumed.</summary>
        SpecialActivated,
        /// <summary>The piece at Pos fell to To.</summary>
        Fell,
        /// <summary>A new piece (Piece) entered the board at Pos.</summary>
        Spawned,
        /// <summary>No legal move existed; the normal pieces were rearranged.</summary>
        Shuffled,
    }

    public enum ObstacleLayer : byte
    {
        None,
        Grass,
        Ice,
        Box,
    }

    /// <summary>
    /// One thing that happened while a move resolved. The presentation layer replays these in
    /// order; <see cref="Phase"/> groups them so it can animate all clears, then all falls, then
    /// the next cascade. Phase 0 is the move itself; each gravity pass starts a new phase.
    /// </summary>
    public readonly struct BoardEvent
    {
        public readonly BoardEventKind Kind;
        public readonly GridPos Pos;
        public readonly GridPos To;
        public readonly Piece Piece;
        public readonly ObstacleLayer Layer;
        public readonly int Phase;

        public BoardEvent(BoardEventKind kind, GridPos pos, GridPos to, Piece piece, ObstacleLayer layer, int phase)
        {
            Kind = kind;
            Pos = pos;
            To = to;
            Piece = piece;
            Layer = layer;
            Phase = phase;
        }

        public override string ToString()
        {
            switch (Kind)
            {
                case BoardEventKind.Swap: return "p" + Phase + " swap " + Pos + "<->" + To;
                case BoardEventKind.Cleared: return "p" + Phase + " clear " + Piece + "@" + Pos;
                case BoardEventKind.ObstacleHit: return "p" + Phase + " hit " + Layer + "@" + Pos;
                case BoardEventKind.BoxDestroyed: return "p" + Phase + " box destroyed@" + Pos;
                case BoardEventKind.SpecialCreated: return "p" + Phase + " create " + Piece + "@" + Pos;
                case BoardEventKind.SpecialActivated: return "p" + Phase + " fire " + Piece + "@" + Pos;
                case BoardEventKind.Fell: return "p" + Phase + " fall " + Pos + "->" + To;
                case BoardEventKind.Spawned: return "p" + Phase + " spawn " + Piece + "@" + Pos;
                case BoardEventKind.Shuffled: return "p" + Phase + " shuffle";
                default: return Kind.ToString();
            }
        }
    }
}
