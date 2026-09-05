using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Match3Lab.Core.Simulation
{
    public sealed class SimulationOptions
    {
        /// <summary>How many games to play. Seeds are FirstSeed, FirstSeed+1, … so runs are reproducible and extendable.</summary>
        public int Runs = 1000;
        public ulong FirstSeed = 1;
        /// <summary>1 forces a plain loop (use on platforms without threads).</summary>
        public int MaxDegreeOfParallelism = Environment.ProcessorCount;
    }

    /// <summary>Aggregate outcome of many games of one level by one bot. Every field is a plain count, so
    /// results are identical regardless of how the runs were scheduled across threads.</summary>
    public sealed class SimulationResult
    {
        public string LevelName;
        public string BotName;
        public int Runs;
        public int Wins;
        public int Losses;
        public long TotalMovesPlayed;
        public long TotalCascades;
        public long TotalShuffles;
        public long TotalScore;
        /// <summary>Index = moves left when the game was won.</summary>
        public int[] MovesLeftWhenWon;
        /// <summary>Per goal: how many runs completed it (won or not).</summary>
        public int[] GoalCompletions;
        /// <summary>Per goal: sum over runs of (done / target) capped at 1, ×10000 for integer storage.</summary>
        public long[] GoalFractionBasisPoints;
        public long ElapsedMilliseconds;

        public double WinRate => Runs == 0 ? 0 : (double)Wins / Runs;
        public double AverageMovesPlayed => Runs == 0 ? 0 : (double)TotalMovesPlayed / Runs;
        public double AverageCascadesPerMove => TotalMovesPlayed == 0 ? 0 : (double)TotalCascades / TotalMovesPlayed;
        public double AverageScore => Runs == 0 ? 0 : (double)TotalScore / Runs;
        public double RunsPerSecond => ElapsedMilliseconds == 0 ? 0 : Runs * 1000.0 / ElapsedMilliseconds;

        public double AverageMovesLeftWhenWon
        {
            get
            {
                if (Wins == 0) return 0;
                long sum = 0;
                for (int i = 0; i < MovesLeftWhenWon.Length; i++) sum += (long)i * MovesLeftWhenWon[i];
                return (double)sum / Wins;
            }
        }

        public double GoalCompletionRate(int goalIndex) => Runs == 0 ? 0 : (double)GoalCompletions[goalIndex] / Runs;
        public double AverageGoalFraction(int goalIndex) => Runs == 0 ? 0 : GoalFractionBasisPoints[goalIndex] / (10000.0 * Runs);

        /// <summary>Standard error of the win rate — the number to read before calling two levels "different".</summary>
        public double WinRateStandardError => Runs == 0 ? 0 : Math.Sqrt(WinRate * (1 - WinRate) / Runs);

        public string Summary()
        {
            var sb = new StringBuilder();
            sb.Append(LevelName).Append(" · ").Append(BotName).Append(" · ").Append(Runs).Append(" runs\n");
            sb.Append("  win rate        ").Append((WinRate * 100).ToString("F1")).Append("% ± ").Append((WinRateStandardError * 100).ToString("F1")).Append('\n');
            sb.Append("  moves left/won  ").Append(AverageMovesLeftWhenWon.ToString("F2")).Append('\n');
            sb.Append("  cascades/move   ").Append(AverageCascadesPerMove.ToString("F2")).Append('\n');
            sb.Append("  avg score       ").Append(AverageScore.ToString("F0")).Append('\n');
            sb.Append("  shuffles        ").Append(TotalShuffles).Append('\n');
            for (int i = 0; i < GoalCompletions.Length; i++)
                sb.Append("  goal ").Append(i).Append(" done     ").Append((GoalCompletionRate(i) * 100).ToString("F1")).Append("%  avg ").Append((AverageGoalFraction(i) * 100).ToString("F0")).Append("%\n");
            sb.Append("  speed           ").Append(RunsPerSecond.ToString("F0")).Append(" runs/s (").Append(ElapsedMilliseconds).Append(" ms)\n");
            return sb.ToString();
        }
    }

    /// <summary>Plays a level many times with a bot and aggregates what happened.</summary>
    public static class Simulator
    {
        public static SimulationResult Run(LevelDefinition level, Func<IBot> botFactory, SimulationOptions options = null)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (botFactory == null) throw new ArgumentNullException(nameof(botFactory));
            options = options ?? new SimulationOptions();
            level.EnsureValid();

            var total = NewResult(level, botFactory().Name, options.Runs);
            var watch = Stopwatch.StartNew();

            if (options.MaxDegreeOfParallelism <= 1)
            {
                var bot = botFactory();
                var buffer = new List<Move>();
                for (int i = 0; i < options.Runs; i++)
                    PlayOne(level, bot, options.FirstSeed + (ulong)i, total, buffer);
            }
            else
            {
                var parallel = new ParallelOptions { MaxDegreeOfParallelism = options.MaxDegreeOfParallelism };
                var gate = new object();
                Parallel.For(0, options.Runs, parallel,
                    () => new Worker(botFactory(), NewResult(level, "", 0)),
                    (i, state, worker) =>
                    {
                        PlayOne(level, worker.Bot, options.FirstSeed + (ulong)i, worker.Partial, worker.Buffer);
                        return worker;
                    },
                    worker => { lock (gate) Merge(total, worker.Partial); });
            }

            watch.Stop();
            total.ElapsedMilliseconds = watch.ElapsedMilliseconds;
            return total;
        }

        private sealed class Worker
        {
            public readonly IBot Bot;
            public readonly SimulationResult Partial;
            public readonly List<Move> Buffer = new List<Move>();

            public Worker(IBot bot, SimulationResult partial)
            {
                Bot = bot;
                Partial = partial;
            }
        }

        private static SimulationResult NewResult(LevelDefinition level, string botName, int runs)
        {
            return new SimulationResult
            {
                LevelName = string.IsNullOrEmpty(level.Name) ? "(unnamed)" : level.Name,
                BotName = botName,
                Runs = runs,
                MovesLeftWhenWon = new int[level.Moves + 1],
                GoalCompletions = new int[level.Goals.Count],
                GoalFractionBasisPoints = new long[level.Goals.Count],
            };
        }

        private static void PlayOne(LevelDefinition level, IBot bot, ulong seed, SimulationResult acc, List<Move> buffer)
        {
            var game = Game.Start(level, seed);
            // A separate stream for the bot's own choices, so changing the bot never changes the board's RNG.
            var botRng = new Pcg32(seed, 0x5EEDBEEF);

            while (game.Status == GameStatus.Playing)
            {
                var moves = game.LegalMoves(buffer);
                if (moves.Count == 0) break; // EnsureMovesExist marks such games lost; defensive only
                var move = bot.Choose(game, moves, botRng);
                var result = game.Play(move);
                acc.TotalCascades += result.Cascades;
            }

            acc.TotalMovesPlayed += game.MovesPlayed;
            acc.TotalShuffles += game.ShuffleCount;
            acc.TotalScore += game.Score;
            if (game.Status == GameStatus.Won)
            {
                acc.Wins++;
                acc.MovesLeftWhenWon[game.MovesLeft]++;
            }
            else
            {
                acc.Losses++;
            }
            for (int i = 0; i < game.Goals.Count; i++)
            {
                var g = game.Goals[i];
                if (g.IsComplete) acc.GoalCompletions[i]++;
                int done = g.Done < g.Goal.Target ? g.Done : g.Goal.Target;
                acc.GoalFractionBasisPoints[i] += (long)done * 10000 / g.Goal.Target;
            }
        }

        private static void Merge(SimulationResult into, SimulationResult part)
        {
            into.Wins += part.Wins;
            into.Losses += part.Losses;
            into.TotalMovesPlayed += part.TotalMovesPlayed;
            into.TotalCascades += part.TotalCascades;
            into.TotalShuffles += part.TotalShuffles;
            into.TotalScore += part.TotalScore;
            for (int i = 0; i < into.MovesLeftWhenWon.Length; i++) into.MovesLeftWhenWon[i] += part.MovesLeftWhenWon[i];
            for (int i = 0; i < into.GoalCompletions.Length; i++)
            {
                into.GoalCompletions[i] += part.GoalCompletions[i];
                into.GoalFractionBasisPoints[i] += part.GoalFractionBasisPoints[i];
            }
        }
    }
}
