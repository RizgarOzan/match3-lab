using System.Collections.Generic;

namespace Match3Lab.Core
{
    /// <summary>Move legality and enumeration.</summary>
    public sealed partial class Game
    {
        public bool IsLegal(Move move)
        {
            if (Status != GameStatus.Playing) return false;
            if (!Board.InBounds(move.A) || !Board.InBounds(move.B)) return false;

            if (move.Kind == MoveKind.Tap)
            {
                ref var c = ref Board[move.A];
                return c.CanSwap && c.Piece.IsSpecial;
            }

            if (!move.A.IsAdjacentTo(move.B)) return false;
            ref var ca = ref Board[move.A];
            ref var cb = ref Board[move.B];
            if (!ca.CanSwap || !cb.CanSwap) return false;
            if (IsSpecialCombo(ca.Piece, cb.Piece)) return true;
            return SwapMakesLine(move.A, move.B);
        }

        /// <summary>Rainbow with anything, or two rockets/bombs, resolves without needing a line.</summary>
        private static bool IsSpecialCombo(Piece a, Piece b)
        {
            if (a.Type == PieceType.Rainbow || b.Type == PieceType.Rainbow) return true;
            return a.IsSpecial && b.IsSpecial;
        }

        private bool SwapMakesLine(GridPos a, GridPos b)
        {
            ref var ca = ref Board[a];
            ref var cb = ref Board[b];
            var pa = ca.Piece;
            var pb = cb.Piece;
            ca.Piece = pb;
            cb.Piece = pa;
            bool line = MatchFinder.LineThrough(Board, a) || MatchFinder.LineThrough(Board, b);
            ca.Piece = pa;
            cb.Piece = pb;
            return line;
        }

        /// <summary>Every legal move, in a fixed order: for each cell (row-major) the swap to the
        /// right, the swap downward, then the tap. Deterministic so bots are reproducible.</summary>
        public List<Move> LegalMoves(List<Move> buffer = null)
        {
            var moves = buffer ?? new List<Move>();
            moves.Clear();
            if (Status != GameStatus.Playing) return moves;

            for (int y = 0; y < Board.Height; y++)
            {
                for (int x = 0; x < Board.Width; x++)
                {
                    var p = new GridPos(x, y);
                    ref var c = ref Board[p];
                    if (!c.CanSwap) continue;

                    if (x + 1 < Board.Width)
                    {
                        var m = Move.Swap(p, new GridPos(x + 1, y));
                        if (IsLegal(m)) moves.Add(m);
                    }
                    if (y + 1 < Board.Height)
                    {
                        var m = Move.Swap(p, new GridPos(x, y + 1));
                        if (IsLegal(m)) moves.Add(m);
                    }
                    if (c.Piece.IsSpecial) moves.Add(Move.Tap(p));
                }
            }
            return moves;
        }

        public bool HasAnyLegalMove()
        {
            for (int y = 0; y < Board.Height; y++)
            {
                for (int x = 0; x < Board.Width; x++)
                {
                    var p = new GridPos(x, y);
                    ref var c = ref Board[p];
                    if (!c.CanSwap) continue;
                    if (c.Piece.IsSpecial) return true;
                    if (x + 1 < Board.Width && IsLegal(Move.Swap(p, new GridPos(x + 1, y)))) return true;
                    if (y + 1 < Board.Height && IsLegal(Move.Swap(p, new GridPos(x, y + 1)))) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Rearranges the normal, unfrozen pieces until at least one legal move exists — preferring
        /// an arrangement with no ready-made lines. Gives up after a bounded number of attempts and
        /// marks the game lost, which only happens on degenerate layouts.
        /// </summary>
        private void EnsureMovesExist()
        {
            if (Status != GameStatus.Playing || HasAnyLegalMove()) return;

            const int maxAttempts = 100;
            var positions = new List<GridPos>();
            var pieces = new List<Piece>();
            foreach (var p in Board.Positions())
            {
                ref var c = ref Board[p];
                if (c.CanSwap && c.Piece.IsNormal)
                {
                    positions.Add(p);
                    pieces.Add(c.Piece);
                }
            }

            bool found = false;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                for (int i = pieces.Count - 1; i > 0; i--)
                {
                    int j = _rng.NextInt(i + 1);
                    var tmp = pieces[i];
                    pieces[i] = pieces[j];
                    pieces[j] = tmp;
                }
                for (int i = 0; i < positions.Count; i++) Board[positions[i]].Piece = pieces[i];

                if (!HasAnyLegalMove()) continue;
                found = true;
                if (_matchFinder.FindAll(Board).Count == 0) break;
            }

            ShuffleCount++;
            Emit(BoardEventKind.Shuffled, default, default, default, ObstacleLayer.None);
            if (!found) Status = GameStatus.Lost;
        }
    }
}
