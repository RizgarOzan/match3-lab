using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Match3Lab.Core.Simulation
{
    /// <summary>
    /// Finds the move budget that lands a level inside a target win-rate band. Win rate is
    /// monotone in the move budget (an extra move can never make a level harder), so a binary
    /// search over the budget converges in a handful of simulations. This is the smallest useful
    /// version of "let the bots tune the level": the layout stays the designer's, only the dial
    /// they would otherwise turn by hand is turned for them.
    /// </summary>
    public static class MoveBudgetTuner
    {
        public sealed class Step
        {
            public int Moves;
            public double WinRate;
            public override string ToString() => Moves + " moves → " + (WinRate * 100).ToString("F1", CultureInfo.InvariantCulture) + "%";
        }

        public sealed class Result
        {
            public int OriginalMoves;
            public int RecommendedMoves;
            public double WinRate;
            public bool InBand;
            public string Note = "";
            public readonly List<Step> Steps = new List<Step>();

            public string Summary()
            {
                var sb = new StringBuilder();
                foreach (var s in Steps) sb.Append("  ").Append(s).Append('\n');
                sb.Append(InBand
                    ? "recommend " + RecommendedMoves + " moves (" + (WinRate * 100).ToString("F1", CultureInfo.InvariantCulture) + "%)"
                    : "no budget lands in band: " + Note);
                if (RecommendedMoves != OriginalMoves) sb.Append("  [was " + OriginalMoves + "]");
                return sb.ToString();
            }
        }

        public static Result Tune(LevelDefinition level, double bandLow, double bandHigh, Func<IBot> botFactory,
            SimulationOptions options = null, int minMoves = 1, int maxMoves = 60)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (bandLow < 0 || bandHigh > 1 || bandLow > bandHigh) throw new ArgumentOutOfRangeException(nameof(bandLow), "band must satisfy 0 <= low <= high <= 1");
            options = options ?? new SimulationOptions { Runs = 400 };

            var result = new Result { OriginalMoves = level.Moves };
            var cache = new Dictionary<int, double>();

            double Measure(int moves)
            {
                if (!cache.TryGetValue(moves, out double rate))
                {
                    rate = Simulator.Run(level.WithMoves(moves), botFactory, options).WinRate;
                    cache[moves] = rate;
                    result.Steps.Add(new Step { Moves = moves, WinRate = rate });
                }
                return rate;
            }

            // Start from the designer's own number: if it is already in band, say so and stop.
            double atOriginal = Measure(level.Moves);
            if (atOriginal >= bandLow && atOriginal <= bandHigh)
            {
                result.RecommendedMoves = level.Moves;
                result.WinRate = atOriginal;
                result.InBand = true;
                return result;
            }

            if (Measure(maxMoves) < bandLow)
            {
                result.RecommendedMoves = maxMoves;
                result.WinRate = cache[maxMoves];
                result.Note = "even " + maxMoves + " moves only reaches " + (cache[maxMoves] * 100).ToString("F1", CultureInfo.InvariantCulture) + "% — the layout, not the budget, is the problem";
                return result;
            }
            if (Measure(minMoves) > bandHigh)
            {
                result.RecommendedMoves = minMoves;
                result.WinRate = cache[minMoves];
                result.Note = "even " + minMoves + " move wins " + (cache[minMoves] * 100).ToString("F1", CultureInfo.InvariantCulture) + "% — the goals are too small for the board";
                return result;
            }

            // Smallest budget whose win rate reaches the low edge of the band.
            int lo = minMoves, hi = maxMoves;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (Measure(mid) >= bandLow) hi = mid; else lo = mid + 1;
            }
            double rateAtLo = Measure(lo);
            result.RecommendedMoves = lo;
            result.WinRate = rateAtLo;
            result.InBand = rateAtLo <= bandHigh;
            if (!result.InBand)
                result.Note = "the win rate jumps from " + (Measure(lo - 1) * 100).ToString("F1", CultureInfo.InvariantCulture) + "% to " + (rateAtLo * 100).ToString("F1", CultureInfo.InvariantCulture) + "% between " + (lo - 1) + " and " + lo + " moves; the band is narrower than one move's worth of difficulty";
            return result;
        }
    }
}
