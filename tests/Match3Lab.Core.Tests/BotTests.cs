using System.Linq;
using Match3Lab.Core;
using Match3Lab.Core.Simulation;
using Xunit;

namespace Match3Lab.Core.Tests
{
    public class BotTests
    {
        private static LevelDefinition Level()
        {
            return LevelText.Parse("name bot\nsize 6 6\nmoves 12\ncolors 4\ngoal color 0 18\ngrid\n" +
                                   string.Concat(Enumerable.Repeat(". . . . . .\n", 6)));
        }

        [Fact]
        public void CloneWithUnknownFuture_keeps_the_board_but_not_the_spawn_stream()
        {
            var game = Game.Start(Level(), 99);
            var exact = game.Clone();
            var blind = game.CloneWithUnknownFuture(1234);
            var move = game.LegalMoves()[0];

            Assert.Equal(game.Board.Dump(), blind.Board.Dump());

            var real = game.Play(move);
            var exactResult = exact.Play(move);
            var blindResult = blind.Play(move);

            Assert.Equal(real.Events.Select(e => e.ToString()), exactResult.Events.Select(e => e.ToString()));
            var realSpawns = real.Events.Where(e => e.Kind == BoardEventKind.Spawned).Select(e => e.Piece.Color).ToArray();
            var blindSpawns = blindResult.Events.Where(e => e.Kind == BoardEventKind.Spawned).Select(e => e.Piece.Color).ToArray();
            Assert.NotEqual(realSpawns, blindSpawns);
        }

        [Fact]
        public void Greedy_choice_is_deterministic_for_a_given_rng()
        {
            var game = Game.Start(Level(), 5);
            var moves = game.LegalMoves();
            var a = new GreedyBot().Choose(game, moves, new Pcg32(42));
            var b = new GreedyBot().Choose(game, moves, new Pcg32(42));

            Assert.Equal(a, b);
            Assert.Contains(a, moves);
        }

        [Fact]
        public void Greedy_takes_an_immediately_winning_move()
        {
            // One colour-0 piece from done; the swap at (2,0)<->(2,1) clears three of them.
            var text = "size 4 3\nmoves 5\ncolors 4\ngoal color 0 3\ngrid\n2 3 0 3\n0 0 1 3\n3 1 3 1\n";
            var game = Game.Start(LevelText.Parse(text), 1);

            var choice = new GreedyBot().Choose(game, game.LegalMoves(), new Pcg32(1));

            Assert.Equal(Move.Swap(2, 0, 2, 1), choice);
        }
    }
}
