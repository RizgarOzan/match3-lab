namespace Match3Lab.Core
{
    /// <summary>
    /// One board cell: a piece plus up to three obstacle layers.
    /// <list type="bullet">
    /// <item><b>Hole</b> — not part of the board at all; nothing falls through it.</item>
    /// <item><b>Box</b> — occupies the cell instead of a piece; loses one hit point per adjacent match or blast.</item>
    /// <item><b>Ice</b> — sits on top of the piece; the piece cannot be swapped until every layer is cleared by matching it.</item>
    /// <item><b>Grass</b> — sits under the piece; cleared when a match or blast happens on this cell.</item>
    /// </list>
    /// A mutable struct on purpose: the board hands out <c>ref Cell</c>, so no copies are made in hot loops.
    /// </summary>
    public struct Cell
    {
        public bool Hole;
        public byte Box;
        public byte Ice;
        public byte Grass;
        public Piece Piece;

        /// <summary>The cell can hold a piece (not a hole, not a box).</summary>
        public bool IsPlayable => !Hole && Box == 0;

        /// <summary>The piece here can take part in a swap.</summary>
        public bool CanSwap => IsPlayable && Ice == 0 && !Piece.IsEmpty;

        public static Cell MakeHole()
        {
            var c = default(Cell);
            c.Hole = true;
            return c;
        }

        public static Cell MakeBox(byte hitPoints)
        {
            var c = default(Cell);
            c.Box = hitPoints;
            return c;
        }
    }
}
