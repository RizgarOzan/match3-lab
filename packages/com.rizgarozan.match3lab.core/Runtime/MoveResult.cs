using System.Collections.Generic;

namespace Match3Lab.Core
{
    /// <summary>What a call to <see cref="Game.Play"/> did. An illegal move produces no events and no state change.</summary>
    public sealed class MoveResult
    {
        public Move Move { get; }
        public bool Legal { get; internal set; }
        public List<BoardEvent> Events { get; } = new List<BoardEvent>();
        /// <summary>Number of match phases after the first one (0 = no chain reaction).</summary>
        public int Cascades { get; internal set; }
        public int ScoreGained { get; internal set; }
        public GameStatus StatusAfter { get; internal set; }

        internal MoveResult(Move move)
        {
            Move = move;
        }

        public int Count(BoardEventKind kind)
        {
            int n = 0;
            for (int i = 0; i < Events.Count; i++)
                if (Events[i].Kind == kind) n++;
            return n;
        }

        public int Count(BoardEventKind kind, int phase)
        {
            int n = 0;
            for (int i = 0; i < Events.Count; i++)
                if (Events[i].Kind == kind && Events[i].Phase == phase) n++;
            return n;
        }

        public override string ToString()
        {
            if (!Legal) return Move + ": illegal";
            return Move + ": " + Events.Count + " events, " + Cascades + " cascades, +" + ScoreGained + ", " + StatusAfter;
        }
    }
}
