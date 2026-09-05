using System;
using System.Collections.Generic;

namespace Match3Lab.Core
{
    public enum GameStatus : byte
    {
        Playing,
        Won,
        Lost,
    }

    public sealed class GoalProgress
    {
        public Goal Goal { get; }
        public int Done { get; internal set; }
        public int Remaining => Math.Max(0, Goal.Target - Done);
        public bool IsComplete => Done >= Goal.Target;

        internal GoalProgress(Goal goal, int done = 0)
        {
            Goal = goal;
            Done = done;
        }

        public override string ToString() => Goal + " (" + Done + "/" + Goal.Target + ")";
    }

    /// <summary>
    /// One play-through of a level: the live board, the move budget, goal progress and the
    /// resolution rules. Everything is deterministic given (level, seed, moves played), which is
    /// what lets the simulator replay a game and the tests pin exact outcomes.
    /// </summary>
    public sealed partial class Game
    {
        public LevelDefinition Level { get; }
        public ulong Seed { get; }
        public Board Board { get; }
        public int MovesLeft { get; private set; }
        public int MovesPlayed { get; private set; }
        public int Score { get; private set; }
        public int ShuffleCount { get; private set; }
        public GameStatus Status { get; private set; }
        public IReadOnlyList<GoalProgress> Goals => _goals;

        private readonly GoalProgress[] _goals;
        private readonly Pcg32 _rng;
        private readonly MatchFinder _matchFinder = new MatchFinder();

        private Game(LevelDefinition level, ulong seed)
        {
            Level = level;
            Seed = seed;
            Board = new Board(level.Width, level.Height);
            MovesLeft = level.Moves;
            _rng = new Pcg32(seed);
            _goals = new GoalProgress[level.Goals.Count];
            for (int i = 0; i < _goals.Length; i++) _goals[i] = new GoalProgress(level.Goals[i]);
        }

        private Game(Game other, Pcg32 rng)
        {
            Level = other.Level;
            Seed = other.Seed;
            Board = other.Board.Clone();
            MovesLeft = other.MovesLeft;
            MovesPlayed = other.MovesPlayed;
            Score = other.Score;
            ShuffleCount = other.ShuffleCount;
            Status = other.Status;
            _rng = rng;
            _goals = new GoalProgress[other._goals.Length];
            for (int i = 0; i < _goals.Length; i++) _goals[i] = new GoalProgress(other._goals[i].Goal, other._goals[i].Done);
        }

        /// <summary>Builds the starting board. The level must be valid.</summary>
        public static Game Start(LevelDefinition level, ulong seed)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            level.EnsureValid();
            var game = new Game(level, seed);
            game.BuildInitialBoard();
            game.EnsureMovesExist();
            return game;
        }

        /// <summary>An exact deep copy: same board, same future spawns. For replays and save states.</summary>
        public Game Clone() => new Game(this, _rng.Clone());

        /// <summary>
        /// A deep copy whose future spawns come from <paramref name="futureSeed"/> instead of the
        /// real stream. Bots that look ahead must use this one: cloning the RNG as well would let
        /// them see exactly which pieces will fall, which no player can.
        /// </summary>
        public Game CloneWithUnknownFuture(ulong futureSeed) => new Game(this, new Pcg32(futureSeed, 0xF07EC457));

        public bool AllGoalsComplete
        {
            get
            {
                for (int i = 0; i < _goals.Length; i++)
                    if (!_goals[i].IsComplete) return false;
                return true;
            }
        }

        private void BuildInitialBoard()
        {
            for (int y = 0; y < Level.Height; y++)
            {
                for (int x = 0; x < Level.Width; x++)
                {
                    var spec = Level.GetCell(x, y);
                    ref var c = ref Board[x, y];
                    switch (spec.Kind)
                    {
                        case CellSpecKind.Hole:
                            c = Cell.MakeHole();
                            continue;
                        case CellSpecKind.Box:
                            c = Cell.MakeBox(spec.BoxHitPoints);
                            c.Grass = spec.Grass;
                            continue;
                        case CellSpecKind.Empty:
                            c.Piece = Piece.Empty;
                            break;
                        case CellSpecKind.FixedColor:
                            c.Piece = Piece.Normal(spec.Color);
                            break;
                        case CellSpecKind.Random:
                            c.Piece = Piece.Normal(PickNonMatchingColor(x, y));
                            break;
                    }
                    c.Ice = spec.Ice;
                    c.Grass = spec.Grass;
                }
            }

            // Empty cells in the layout are filled by an initial gravity pass, without events.
            _events.Clear();
            ApplyGravity();
            _events.Clear();
        }

        /// <summary>A colour that does not complete a line of three with the cells already placed to the left and above.</summary>
        private byte PickNonMatchingColor(int x, int y)
        {
            int colors = Level.ColorCount;
            int excludedMask = 0;
            if (x >= 2 && SameNormalColor(x - 1, y, x - 2, y, out byte left)) excludedMask |= 1 << left;
            if (y >= 2 && SameNormalColor(x, y - 1, x, y - 2, out byte up)) excludedMask |= 1 << up;

            int allowed = 0;
            for (int c = 0; c < colors; c++)
                if ((excludedMask & (1 << c)) == 0) allowed++;

            int pick = _rng.NextInt(allowed);
            for (int c = 0; c < colors; c++)
            {
                if ((excludedMask & (1 << c)) != 0) continue;
                if (pick == 0) return (byte)c;
                pick--;
            }
            return 0; // unreachable: ColorCount >= 3 leaves at least one colour allowed
        }

        private bool SameNormalColor(int x1, int y1, int x2, int y2, out byte color)
        {
            ref var a = ref Board[x1, y1];
            ref var b = ref Board[x2, y2];
            color = a.Piece.Color;
            return a.IsPlayable && b.IsPlayable && a.Piece.IsNormal && b.Piece.IsNormal && a.Piece.Color == b.Piece.Color;
        }

        /// <summary>The most numerous normal colour on the board; ties go to the lowest index.</summary>
        public byte MostCommonColor()
        {
            var counts = new int[LevelDefinition.MaxColors];
            for (int i = 0; i < Board.CellCount; i++)
            {
                ref var c = ref Board[i % Board.Width, i / Board.Width];
                if (c.IsPlayable && c.Piece.IsNormal) counts[c.Piece.Color]++;
            }
            int best = 0;
            for (int i = 1; i < counts.Length; i++)
                if (counts[i] > counts[best]) best = i;
            return (byte)best;
        }
    }
}
