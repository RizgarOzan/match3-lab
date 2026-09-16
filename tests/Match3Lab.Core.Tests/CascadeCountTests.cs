using System.Linq;
using Match3Lab.Core;
using Xunit;

namespace Match3Lab.Core.Tests
{
    public class CascadeCountTests
    {
        /// <summary>
        /// A cascade is a match pass that gravity caused. Phase 0 is the move itself (swap match, tap
        /// or special combination); every later phase that removes or damages anything is one cascade.
        /// So Cascades must equal the last phase with a clear, hit or activation — for every move kind.
        /// </summary>
        [Fact]
        public void Cascades_equals_the_last_phase_that_resolved_something_for_swaps_taps_and_combinations()
        {
            var level = LevelText.Parse("name cascades\nsize 6 6\nmoves 400\ncolors 4\ngoal color 0 9999\ngrid\n" +
                                        string.Concat(Enumerable.Repeat(". . . . . .\n", 6)));
            int taps = 0, combos = 0, swaps = 0, chained = 0;

            for (ulong seed = 1; seed <= 20; seed++)
            {
                var game = Game.Start(level, seed);
                var rng = new Pcg32(seed * 7919);
                while (game.Status == GameStatus.Playing)
                {
                    var moves = game.LegalMoves();
                    // Prefer taps and special combinations so both kinds are well covered.
                    var special = moves.Where(m => m.Kind == MoveKind.Tap ||
                                                   (game.Board[m.A].Piece.IsSpecial && game.Board[m.B].Piece.IsSpecial)).ToList();
                    var move = special.Count > 0 ? special[rng.NextInt(special.Count)] : moves[rng.NextInt(moves.Count)];
                    bool combo = move.Kind != MoveKind.Tap && game.Board[move.A].Piece.IsSpecial && game.Board[move.B].Piece.IsSpecial;

                    var result = game.Play(move);

                    int lastPhase = result.Events
                        .Where(e => e.Kind == BoardEventKind.Cleared || e.Kind == BoardEventKind.ObstacleHit ||
                                    e.Kind == BoardEventKind.BoxDestroyed || e.Kind == BoardEventKind.SpecialActivated)
                        .Select(e => e.Phase).DefaultIfEmpty(0).Max();
                    Assert.True(lastPhase == result.Cascades, move + ": Cascades " + result.Cascades + ", last resolving phase " + lastPhase);

                    if (move.Kind == MoveKind.Tap) taps++;
                    else if (combo) combos++;
                    else swaps++;
                    if (result.Cascades > 0) chained++;
                }
            }

            Assert.True(taps > 20 && combos > 0 && swaps > 20 && chained > 20, "taps " + taps + ", combos " + combos + ", swaps " + swaps + ", chained " + chained);
        }
    }
}
