using System.Linq;
using Match3Lab.Core;
using Xunit;

namespace Match3Lab.Core.Tests
{
    public class RulesTests
    {
        /// <summary>Builds a game from a grid of fixed colours so every test starts from a known board.</summary>
        private static Game Start(string grid, int moves = 10, int colors = 4, string goals = "goal color 0 99", ulong seed = 1)
        {
            string[] rows = grid.Trim().Split('\n')
                .Select(r => string.Join(" ", r.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries)))
                .ToArray();
            int width = rows[0].Split(' ').Length;
            string text = "size " + width + " " + rows.Length + "\nmoves " + moves + "\ncolors " + colors + "\n" + goals + "\ngrid\n" + string.Join("\n", rows) + "\n";
            return Game.Start(LevelText.Parse(text), seed);
        }

        [Fact]
        public void Three_in_a_row_clears_refills_and_spends_a_move()
        {
            var game = Start(@"
                2 3 0 3
                0 0 1 3
                3 1 3 1");

            var result = game.Play(Move.Swap(2, 0, 2, 1));

            Assert.True(result.Legal);
            Assert.Equal(9, game.MovesLeft);
            Assert.Equal(3, result.Count(BoardEventKind.Cleared, phase: 0));
            Assert.Equal(3, result.Count(BoardEventKind.Fell, phase: 1));
            Assert.Equal(3, result.Count(BoardEventKind.Spawned, phase: 1));
            Assert.True(game.Goals[0].Done >= 3);
            foreach (var p in game.Board.Positions())
                Assert.False(game.Board[p].Piece.IsEmpty);
        }

        [Fact]
        public void A_swap_that_makes_no_line_is_rejected_without_side_effects()
        {
            var game = Start(@"
                2 3 0 3
                0 0 1 3
                3 1 3 1");
            string before = game.Board.Dump();

            var result = game.Play(Move.Swap(0, 0, 1, 0));

            Assert.False(result.Legal);
            Assert.Empty(result.Events);
            Assert.Equal(10, game.MovesLeft);
            Assert.Equal(before, game.Board.Dump());
        }

        [Fact]
        public void Four_in_a_row_creates_a_rocket_at_the_dropped_cell_and_a_tap_fires_it()
        {
            var game = Start(@"
                2 3 0 3
                0 0 1 0
                3 1 3 1");

            var result = game.Play(Move.Swap(2, 0, 2, 1));

            Assert.Equal(4, result.Count(BoardEventKind.Cleared, phase: 0));
            var created = result.Events.Single(e => e.Kind == BoardEventKind.SpecialCreated);
            Assert.Equal(new GridPos(2, 1), created.Pos);
            Assert.Equal(PieceType.RocketV, created.Piece.Type);
            Assert.Equal(PieceType.RocketV, game.Board[2, 1].Piece.Type);

            var tap = game.Play(Move.Tap(2, 1));

            Assert.True(tap.Legal);
            var fired = tap.Events.Single(e => e.Kind == BoardEventKind.SpecialActivated);
            Assert.Equal(new GridPos(2, 1), fired.Pos);
            Assert.Equal(2, tap.Count(BoardEventKind.Cleared, phase: 0));
            Assert.All(tap.Events.Where(e => e.Kind == BoardEventKind.Cleared && e.Phase == 0), e => Assert.Equal(2, e.Pos.X));
        }

        [Fact]
        public void An_L_shape_creates_a_bomb_at_the_corner()
        {
            var game = Start(@"
                0 3 2 1
                0 2 3 1
                1 0 0 2
                0 1 2 3");

            var result = game.Play(Move.Swap(0, 3, 0, 2));

            Assert.Equal(5, result.Count(BoardEventKind.Cleared, phase: 0));
            var created = result.Events.Single(e => e.Kind == BoardEventKind.SpecialCreated);
            Assert.Equal(PieceType.Bomb, created.Piece.Type);
            Assert.Equal(new GridPos(0, 2), created.Pos);
        }

        [Fact]
        public void Five_in_a_row_creates_a_rainbow()
        {
            var game = Start(@"
                0 0 1 0 0
                2 3 0 2 3
                1 2 3 1 2");

            var result = game.Play(Move.Swap(2, 1, 2, 0));

            var created = result.Events.Single(e => e.Kind == BoardEventKind.SpecialCreated);
            Assert.Equal(PieceType.Rainbow, created.Piece.Type);
            Assert.Equal(new GridPos(2, 0), created.Pos);
        }

        [Fact]
        public void Obstacles_grass_ice_and_box_each_follow_their_rule_and_goals_win_the_level()
        {
            var game = Start(@"
                2  3  0 3
                0g 0i 1 3
                3  1  b 1",
                goals: "goal color 0 2\ngoal grass 1\ngoal ice 1\ngoal box 1");

            var result = game.Play(Move.Swap(2, 0, 2, 1));

            // (0,1): piece cleared and the grass under it. (1,1): ice removed, piece stays. (2,1): cleared; the box below is hit.
            Assert.Equal(2, result.Count(BoardEventKind.Cleared, phase: 0));
            Assert.Contains(result.Events, e => e.Kind == BoardEventKind.ObstacleHit && e.Layer == ObstacleLayer.Grass && e.Pos == new GridPos(0, 1));
            Assert.Contains(result.Events, e => e.Kind == BoardEventKind.ObstacleHit && e.Layer == ObstacleLayer.Ice && e.Pos == new GridPos(1, 1));
            Assert.Contains(result.Events, e => e.Kind == BoardEventKind.BoxDestroyed && e.Pos == new GridPos(2, 2));
            Assert.Equal(Piece.Normal(0), game.Board[1, 1].Piece);
            Assert.Equal(0, game.Board[1, 1].Ice);
            Assert.True(game.Board[2, 2].IsPlayable);
            Assert.False(game.Board[2, 2].Piece.IsEmpty);
            Assert.All(game.Goals, g => Assert.True(g.IsComplete, g.ToString()));
            Assert.Equal(GameStatus.Won, game.Status);
            Assert.False(game.Play(Move.Swap(0, 0, 1, 0)).Legal);
        }

        [Fact]
        public void A_blast_crossing_an_iced_grass_cell_peels_both_ice_and_grass_but_the_piece_survives()
        {
            // Row 1 becomes 0 0 0 0 after the swap -> a rocket lands at the dropped cell (2,1).
            // (2,3) is grass-2 over ice-2 over colour 0 and is frozen floor, so it stays put.
            var game = Start(@"
                2 3 0   3
                0 0 1   0
                3 1 3   1
                1 2 0GI 2",
                goals: "goal ice 2\ngoal grass 2");
            game.Play(Move.Swap(2, 0, 2, 1));
            Assert.Equal(PieceType.RocketV, game.Board[2, 1].Piece.Type);

            var tap = game.Play(Move.Tap(2, 1));

            // ADR 0002: the blast crosses (2,3); ice shields the piece but the grass under it still loses a layer.
            Assert.Contains(tap.Events, e => e.Kind == BoardEventKind.ObstacleHit && e.Layer == ObstacleLayer.Ice && e.Pos == new GridPos(2, 3) && e.Phase == 0);
            Assert.Contains(tap.Events, e => e.Kind == BoardEventKind.ObstacleHit && e.Layer == ObstacleLayer.Grass && e.Pos == new GridPos(2, 3) && e.Phase == 0);
            Assert.Equal(1, game.Board[2, 3].Ice);
            Assert.Equal(1, game.Board[2, 3].Grass);
            Assert.Equal(Piece.Normal(0), game.Board[2, 3].Piece);
        }

        [Fact]
        public void A_match_on_an_iced_grass_cell_peels_the_ice_only_and_leaves_the_grass()
        {
            // The iced piece cannot be swapped; instead a plain 0 slides into (1,1) so column 1
            // becomes 0 0gi 0 down rows 1-3 -- a vertical match whose middle cell is iced+grassed.
            var game = Start(@"
                2 3   1 3
                0 3   2 1
                1 0gi 2 0
                3 0   1 2",
                goals: "goal color 0 99");

            var result = game.Play(Move.Swap(0, 1, 1, 1));

            // A match is not a blast: the ice absorbs the hit and the piece stays, so the grass under
            // it keeps its layer (no Grass hit in the resolving phase). Only the blast case peels grass.
            Assert.Contains(result.Events, e => e.Kind == BoardEventKind.ObstacleHit && e.Layer == ObstacleLayer.Ice && e.Pos == new GridPos(1, 2) && e.Phase == 0);
            Assert.DoesNotContain(result.Events, e => e.Kind == BoardEventKind.ObstacleHit && e.Layer == ObstacleLayer.Grass && e.Pos == new GridPos(1, 2) && e.Phase == 0);
            Assert.Equal(0, game.Board[1, 2].Ice);
        }

        [Fact]
        public void Running_out_of_moves_loses()
        {
            var game = Start(@"
                2 3 0 3
                0 0 1 3
                3 1 3 1", moves: 1);

            var result = game.Play(Move.Swap(2, 0, 2, 1));

            Assert.Equal(GameStatus.Lost, result.StatusAfter);
            Assert.Equal(0, game.MovesLeft);
        }

        [Fact]
        public void Same_seed_and_moves_reproduce_the_same_game()
        {
            var level = LevelText.Parse("name d\nsize 6 6\nmoves 15\ncolors 5\ngoal color 0 30\ngrid\n" +
                                        string.Concat(Enumerable.Repeat(". . . . . .\n", 6)));
            var a = Game.Start(level, 12345);
            var b = Game.Start(level, 12345);
            var c = Game.Start(level, 54321);

            Assert.Equal(a.Board.Dump(), b.Board.Dump());
            Assert.NotEqual(a.Board.Dump(), c.Board.Dump());

            for (int i = 0; i < 10 && a.Status == GameStatus.Playing; i++)
            {
                var moves = a.LegalMoves();
                Assert.NotEmpty(moves);
                var ra = a.Play(moves[0]);
                var rb = b.Play(moves[0]);
                Assert.Equal(ra.Events.Select(e => e.ToString()), rb.Events.Select(e => e.ToString()));
                Assert.Equal(a.Board.Dump(), b.Board.Dump());
            }
        }

        [Fact]
        public void A_board_with_no_moves_is_shuffled_until_one_exists()
        {
            var game = Start(@"
                0 1 2
                1 2 0
                2 0 1", colors: 3);

            Assert.True(game.ShuffleCount >= 1);
            Assert.NotEmpty(game.LegalMoves());
        }

        [Fact]
        public void Clone_plays_independently()
        {
            var game = Start(@"
                2 3 0 3
                0 0 1 3
                3 1 3 1");
            var copy = game.Clone();

            copy.Play(Move.Swap(2, 0, 2, 1));

            Assert.Equal(10, game.MovesLeft);
            Assert.Equal(9, copy.MovesLeft);
            Assert.Equal(Piece.Normal(1), game.Board[2, 1].Piece);
        }

        [Fact]
        public void Legal_moves_are_all_actually_legal_and_nothing_else_is()
        {
            var game = Start(@"
                2 3 0 3 1
                0 0 1 3 2
                3 1 3 1 0
                1 2 0 2 3");

            var legal = game.LegalMoves().ToHashSet();
            foreach (var p in game.Board.Positions())
            {
                var right = Move.Swap(p, p.Offset(1, 0));
                var down = Move.Swap(p, p.Offset(0, 1));
                if (game.Board.InBounds(right.B)) Assert.Equal(legal.Contains(right), game.IsLegal(right));
                if (game.Board.InBounds(down.B)) Assert.Equal(legal.Contains(down), game.IsLegal(down));
            }
            Assert.Contains(Move.Swap(2, 0, 2, 1), legal);
        }
    }
}
