using System.Collections.Generic;

namespace Match3Lab.Core
{
    /// <summary>
    /// The resolution rules. In one sentence: clear what matched, fire what was hit, let gravity
    /// run, and repeat until the board is quiet. See docs/decisions/0002 for the rule choices.
    /// </summary>
    public sealed partial class Game
    {
        private const int ScorePerPiece = 10;
        private const int ScorePerSpecialFired = 20;

        private readonly List<BoardEvent> _events = new List<BoardEvent>();
        private readonly Queue<GridPos> _activations = new Queue<GridPos>();
        private readonly HashSet<GridPos> _queued = new HashSet<GridPos>();
        private readonly HashSet<GridPos> _createdThisPhase = new HashSet<GridPos>();
        private readonly HashSet<GridPos> _boxesHitByGroup = new HashSet<GridPos>();
        private readonly List<GridPos> _blast = new List<GridPos>();
        private int _phase;
        private int _cascade;
        private int _scoreGained;

        private enum HitSource : byte { Match, Blast }

        /// <summary>Applies a move. Illegal moves (and moves after the game ended) change nothing.</summary>
        public MoveResult Play(Move move)
        {
            var result = new MoveResult(move);
            if (!IsLegal(move)) return result;
            result.Legal = true;

            _events.Clear();
            _activations.Clear();
            _queued.Clear();
            _createdThisPhase.Clear();
            _phase = 0;
            _cascade = 0;
            _scoreGained = 0;
            MovesLeft--;
            MovesPlayed++;

            bool needsMatchPass;
            if (move.Kind == MoveKind.Tap)
            {
                EnqueueActivation(move.A);
                needsMatchPass = false;
            }
            else
            {
                needsMatchPass = !ApplySwap(move.A, move.B);
            }

            if (needsMatchPass)
                ProcessMatches(_matchFinder.FindAll(Board), move.B, move.A, true);

            while (true)
            {
                ProcessActivations();
                _phase++;
                ApplyGravity();
                _createdThisPhase.Clear();

                var groups = _matchFinder.FindAll(Board);
                if (groups.Count == 0) break;
                _cascade++;
                ProcessMatches(groups, default, default, false);
            }

            UpdateStatus();
            if (Status == GameStatus.Playing) EnsureMovesExist();

            result.Events.AddRange(_events);
            result.Cascades = _cascade;
            result.ScoreGained = _scoreGained;
            Score += _scoreGained;
            result.StatusAfter = Status;
            return result;
        }

        private void UpdateStatus()
        {
            if (AllGoalsComplete) Status = GameStatus.Won;
            else if (MovesLeft <= 0) Status = GameStatus.Lost;
        }

        // ---- swap and special combinations -------------------------------------------------

        /// <summary>Swaps the pieces; returns true when the swap was a special combination that
        /// already resolved itself (so no ordinary match pass is needed).</summary>
        private bool ApplySwap(GridPos from, GridPos to)
        {
            ref var ca = ref Board[from];
            ref var cb = ref Board[to];
            var moved = ca.Piece;
            var other = cb.Piece;
            ca.Piece = other;
            cb.Piece = moved;
            Emit(BoardEventKind.Swap, from, to, moved, ObstacleLayer.None);

            // After the swap the dragged piece sits at `to`, the other one at `from`.
            if (moved.Type == PieceType.Rainbow) { RainbowCombo(to, from); return true; }
            if (other.Type == PieceType.Rainbow) { RainbowCombo(from, to); return true; }
            if (moved.IsSpecial && other.IsSpecial) { SpecialPairCombo(to, from); return true; }
            return false;
        }

        private void RainbowCombo(GridPos rainbowPos, GridPos otherPos)
        {
            var other = Board[otherPos].Piece;
            ConsumeSpecial(rainbowPos);

            if (other.Type == PieceType.Rainbow)
            {
                ConsumeSpecial(otherPos);
                foreach (var p in Board.Positions())
                    if (Board[p].IsPlayable) HitCell(p, HitSource.Blast);
                return;
            }

            if (other.IsNormal)
            {
                byte color = other.Color;
                foreach (var p in Board.Positions())
                {
                    ref var c = ref Board[p];
                    if (c.IsPlayable && c.Piece.IsNormal && c.Piece.Color == color) HitCell(p, HitSource.Blast);
                }
                return;
            }

            // Rainbow + rocket/bomb: every piece of the most common colour becomes that special and fires.
            byte target = MostCommonColor();
            foreach (var p in Board.Positions())
            {
                ref var c = ref Board[p];
                if (!c.IsPlayable || !c.Piece.IsNormal || c.Piece.Color != target) continue;
                var type = other.Type;
                if (other.IsRocket) type = (p.X + p.Y) % 2 == 0 ? PieceType.RocketH : PieceType.RocketV;
                c.Piece = Piece.Special(type);
                Emit(BoardEventKind.SpecialCreated, p, default, c.Piece, ObstacleLayer.None);
                EnqueueActivation(p);
            }
            EnqueueActivation(otherPos);
        }

        private void SpecialPairCombo(GridPos center, GridPos otherPos)
        {
            var a = Board[center].Piece;
            var b = Board[otherPos].Piece;
            ConsumeSpecial(center);
            ConsumeSpecial(otherPos);

            _blast.Clear();
            bool bothBombs = a.Type == PieceType.Bomb && b.Type == PieceType.Bomb;
            bool bothRockets = a.IsRocket && b.IsRocket;
            if (bothBombs)
            {
                AddSquare(center, 2);
            }
            else if (bothRockets)
            {
                AddRow(center.Y);
                AddColumn(center.X);
            }
            else
            {
                for (int d = -1; d <= 1; d++)
                {
                    if (Board.InBounds(0, center.Y + d)) AddRow(center.Y + d);
                    if (Board.InBounds(center.X + d, 0)) AddColumn(center.X + d);
                }
            }
            HitBlast();
        }

        /// <summary>Removes a special piece as part of a combination (it fires through the combo, not on its own).</summary>
        private void ConsumeSpecial(GridPos p)
        {
            ref var c = ref Board[p];
            var piece = c.Piece;
            c.Piece = Piece.Empty;
            _scoreGained += ScorePerSpecialFired * (1 + _cascade);
            Emit(BoardEventKind.SpecialActivated, p, default, piece, ObstacleLayer.None);
            ClearGrass(ref c, p);
        }

        // ---- matches ----------------------------------------------------------------------

        private void ProcessMatches(List<MatchGroup> groups, GridPos preferredA, GridPos preferredB, bool hasPreferred)
        {
            for (int g = 0; g < groups.Count; g++)
            {
                var group = groups[g];
                var special = SpecialFor(group);
                var anchor = ChooseAnchor(group, special, preferredA, preferredB, hasPreferred);

                _boxesHitByGroup.Clear();
                for (int i = 0; i < group.Cells.Count; i++)
                {
                    var p = group.Cells[i];
                    HitCell(p, HitSource.Match);
                    for (int d = 0; d < GridPos.CardinalOffsets.Length; d++)
                    {
                        var n = p.Offset(GridPos.CardinalOffsets[d].X, GridPos.CardinalOffsets[d].Y);
                        if (Board.InBounds(n) && Board[n].Box > 0 && _boxesHitByGroup.Add(n)) DamageBox(n);
                    }
                }

                if (special == PieceType.Empty) continue;
                if (!TryPlaceSpecial(anchor, special))
                {
                    for (int i = 0; i < group.Cells.Count; i++)
                        if (TryPlaceSpecial(group.Cells[i], special)) break;
                }
            }
        }

        private static PieceType SpecialFor(MatchGroup group)
        {
            if (group.LongestRun >= 5) return PieceType.Rainbow;
            if (group.HasHorizontal && group.HasVertical) return PieceType.Bomb;
            // A rocket fires across the line that made it, so it reaches new territory.
            if (group.LongestRun == 4) return group.LongestRunIsHorizontal ? PieceType.RocketV : PieceType.RocketH;
            return PieceType.Empty;
        }

        private static GridPos ChooseAnchor(MatchGroup group, PieceType special, GridPos a, GridPos b, bool hasPreferred)
        {
            if (hasPreferred)
            {
                if (group.Contains(a)) return a;
                if (group.Contains(b)) return b;
            }
            if (special == PieceType.Bomb && group.HasIntersection) return group.Intersection;
            return group.LongestRunMiddle;
        }

        private bool TryPlaceSpecial(GridPos p, PieceType special)
        {
            ref var c = ref Board[p];
            if (!c.IsPlayable || !c.Piece.IsEmpty) return false;
            c.Piece = Piece.Special(special);
            _createdThisPhase.Add(p);
            Emit(BoardEventKind.SpecialCreated, p, default, c.Piece, ObstacleLayer.None);
            return true;
        }

        // ---- hitting cells ---------------------------------------------------------------

        private void HitCell(GridPos p, HitSource source)
        {
            ref var c = ref Board[p];
            if (c.Hole) return;
            if (c.Box > 0)
            {
                if (source == HitSource.Blast) DamageBox(p);
                return;
            }
            if (c.Ice > 0)
            {
                c.Ice--;
                Progress(GoalKind.Ice, 0);
                Emit(BoardEventKind.ObstacleHit, p, default, c.Piece, ObstacleLayer.Ice);
                // ADR 0002: grass under the cell loses a layer when a blast crosses it, even
                // though the ice shields the piece itself. A match hit is not a blast, so it
                // only peels the ice and the grass stays.
                if (source == HitSource.Blast) ClearGrass(ref c, p);
                return;
            }
            if (c.Piece.IsSpecial)
            {
                if (!_createdThisPhase.Contains(p)) EnqueueActivation(p);
                return;
            }
            if (c.Piece.IsNormal)
            {
                var piece = c.Piece;
                c.Piece = Piece.Empty;
                Progress(GoalKind.Color, piece.Color);
                _scoreGained += ScorePerPiece * (1 + _cascade);
                Emit(BoardEventKind.Cleared, p, default, piece, ObstacleLayer.None);
            }
            ClearGrass(ref c, p);
        }

        private void ClearGrass(ref Cell c, GridPos p)
        {
            if (c.Grass == 0) return;
            c.Grass--;
            Progress(GoalKind.Grass, 0);
            Emit(BoardEventKind.ObstacleHit, p, default, default, ObstacleLayer.Grass);
        }

        private void DamageBox(GridPos p)
        {
            ref var c = ref Board[p];
            if (c.Box == 0) return;
            c.Box--;
            Progress(GoalKind.Box, 0);
            Emit(BoardEventKind.ObstacleHit, p, default, default, ObstacleLayer.Box);
            if (c.Box == 0) Emit(BoardEventKind.BoxDestroyed, p, default, default, ObstacleLayer.Box);
        }

        private void Progress(GoalKind kind, byte color)
        {
            for (int i = 0; i < _goals.Length; i++)
            {
                var g = _goals[i].Goal;
                if (g.Kind == kind && (kind != GoalKind.Color || g.Color == color)) _goals[i].Done++;
            }
        }

        // ---- specials ---------------------------------------------------------------------

        private void EnqueueActivation(GridPos p)
        {
            if (_queued.Add(p)) _activations.Enqueue(p);
        }

        private void ProcessActivations()
        {
            while (_activations.Count > 0)
            {
                var p = _activations.Dequeue();
                _queued.Remove(p);
                ref var c = ref Board[p];
                if (!c.IsPlayable || !c.Piece.IsSpecial) continue;
                var piece = c.Piece;
                ConsumeSpecial(p);

                _blast.Clear();
                switch (piece.Type)
                {
                    case PieceType.RocketH: AddRow(p.Y); break;
                    case PieceType.RocketV: AddColumn(p.X); break;
                    case PieceType.Bomb: AddSquare(p, 1); break;
                    case PieceType.Rainbow:
                        byte color = MostCommonColor();
                        foreach (var q in Board.Positions())
                        {
                            ref var qc = ref Board[q];
                            if (qc.IsPlayable && qc.Piece.IsNormal && qc.Piece.Color == color) _blast.Add(q);
                        }
                        break;
                }
                HitBlast();
            }
        }

        private void AddRow(int y)
        {
            for (int x = 0; x < Board.Width; x++) _blast.Add(new GridPos(x, y));
        }

        private void AddColumn(int x)
        {
            for (int y = 0; y < Board.Height; y++) _blast.Add(new GridPos(x, y));
        }

        private void AddSquare(GridPos center, int radius)
        {
            for (int y = center.Y - radius; y <= center.Y + radius; y++)
                for (int x = center.X - radius; x <= center.X + radius; x++)
                    if (Board.InBounds(x, y)) _blast.Add(new GridPos(x, y));
        }

        private void HitBlast()
        {
            // Copy first: HitCell may enqueue activations, and nested combos reuse _blast.
            var cells = _blast.ToArray();
            for (int i = 0; i < cells.Length; i++) HitCell(cells[i], HitSource.Blast);
        }

        // ---- gravity ----------------------------------------------------------------------

        /// <summary>
        /// Pieces fall straight down within each column segment. Segments are separated by holes,
        /// boxes and frozen (iced) pieces. Every segment refills from its own top, so a pocket under
        /// a box never becomes a permanent void.
        /// </summary>
        private void ApplyGravity()
        {
            for (int x = 0; x < Board.Width; x++)
            {
                int y = Board.Height - 1;
                while (y >= 0)
                {
                    if (IsFloor(x, y)) { y--; continue; }
                    int bottom = y;
                    int top = y;
                    while (top - 1 >= 0 && !IsFloor(x, top - 1)) top--;

                    int write = bottom;
                    for (int read = bottom; read >= top; read--)
                    {
                        ref var rc = ref Board[x, read];
                        if (rc.Piece.IsEmpty) continue;
                        if (read != write)
                        {
                            Board[x, write].Piece = rc.Piece;
                            rc.Piece = Piece.Empty;
                            Emit(BoardEventKind.Fell, new GridPos(x, read), new GridPos(x, write), Board[x, write].Piece, ObstacleLayer.None);
                        }
                        write--;
                    }
                    for (int s = write; s >= top; s--)
                    {
                        var piece = Piece.Normal((byte)_rng.NextInt(Level.ColorCount));
                        Board[x, s].Piece = piece;
                        Emit(BoardEventKind.Spawned, new GridPos(x, s), default, piece, ObstacleLayer.None);
                    }
                    y = top - 1;
                }
            }
        }

        private bool IsFloor(int x, int y)
        {
            ref var c = ref Board[x, y];
            return c.Hole || c.Box > 0 || c.Ice > 0;
        }

        // ---- events -----------------------------------------------------------------------

        private void Emit(BoardEventKind kind, GridPos pos, GridPos to, Piece piece, ObstacleLayer layer)
        {
            _events.Add(new BoardEvent(kind, pos, to, piece, layer, _phase));
        }
    }
}
