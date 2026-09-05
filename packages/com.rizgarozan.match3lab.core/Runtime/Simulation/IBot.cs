using System.Collections.Generic;

namespace Match3Lab.Core.Simulation
{
    /// <summary>
    /// A policy that picks one move from the legal ones. Bots must be deterministic given the
    /// game state and the supplied RNG, so a simulation run can be reproduced from its seed.
    /// </summary>
    public interface IBot
    {
        string Name { get; }
        Move Choose(Game game, List<Move> legalMoves, Pcg32 rng);
    }

    /// <summary>Picks uniformly at random. The floor every other bot is measured against.</summary>
    public sealed class RandomBot : IBot
    {
        public string Name => "random";

        public Move Choose(Game game, List<Move> legalMoves, Pcg32 rng) => legalMoves[rng.NextInt(legalMoves.Count)];
    }

    /// <summary>
    /// Tries every legal move on a copy of the game and keeps the one whose immediate outcome
    /// scores best: goal progress first, then specials created, then pieces cleared. Ties break
    /// randomly. One ply, no search — deliberately a stand-in for a competent, not optimal, player.
    ///
    /// The copy is made with <see cref="Game.CloneWithUnknownFuture"/>: the bot sees the board it
    /// can see, and guesses at the refill like a human would. Cloning the real RNG made an early
    /// version of this bot win levels a human cannot — it was reading tomorrow's spawns.
    /// </summary>
    public sealed class GreedyBot : IBot
    {
        public string Name => "greedy";

        public const int WinBonus = 100_000;
        public const int GoalProgressWeight = 100;
        public const int SpecialCreatedWeight = 15;
        public const int PieceClearedWeight = 1;
        public const int LossPenalty = 50_000;

        public Move Choose(Game game, List<Move> legalMoves, Pcg32 rng)
        {
            Move best = legalMoves[0];
            long bestScore = long.MinValue;
            int ties = 0;
            // One imagined future per decision, shared by every candidate, so moves are compared fairly.
            ulong futureSeed = ((ulong)rng.NextUInt() << 32) | rng.NextUInt();

            for (int i = 0; i < legalMoves.Count; i++)
            {
                long score = Evaluate(game, legalMoves[i], futureSeed);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = legalMoves[i];
                    ties = 1;
                }
                else if (score == bestScore)
                {
                    // Reservoir sampling keeps tie-breaking uniform without a second pass.
                    ties++;
                    if (rng.NextInt(ties) == 0) best = legalMoves[i];
                }
            }
            return best;
        }

        public static long Evaluate(Game game, Move move, ulong futureSeed)
        {
            var copy = game.CloneWithUnknownFuture(futureSeed);
            int progressBefore = UsefulProgress(copy);
            var result = copy.Play(move);
            if (!result.Legal) return long.MinValue;

            long score = 0;
            if (copy.Status == GameStatus.Won) score += WinBonus;
            else if (copy.Status == GameStatus.Lost) score -= LossPenalty;
            score += (UsefulProgress(copy) - progressBefore) * (long)GoalProgressWeight;
            score += result.Count(BoardEventKind.SpecialCreated) * (long)SpecialCreatedWeight;
            score += result.Count(BoardEventKind.Cleared) * (long)PieceClearedWeight;
            return score;
        }

        /// <summary>Goal progress capped at each target, so overshooting a finished goal earns nothing.</summary>
        private static int UsefulProgress(Game game)
        {
            int total = 0;
            for (int i = 0; i < game.Goals.Count; i++)
            {
                var g = game.Goals[i];
                total += g.Done < g.Goal.Target ? g.Done : g.Goal.Target;
            }
            return total;
        }
    }
}
