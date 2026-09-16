using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Match3Lab.Core;
using Match3Lab.Core.Simulation;

namespace Match3Lab.Cli
{
    /// <summary>
    /// Headless front door to the core: validate levels, simulate one, or chart a whole folder.
    /// Everything the Unity editor window will show comes from the same calls.
    /// </summary>
    internal static class Program
    {
        private const string Usage = @"m3lab — Match3 Lab command line

  m3lab validate <level.txt|dir>            check levels and print problems
  m3lab sim <level.txt> [options]           play one level many times
  m3lab curve <dir> [options] [--csv f]     simulate every level in a folder, print the difficulty table
  m3lab check <level.txt|dir> [--runs N]    fail unless every level's greedy win rate is inside its declared band
  m3lab show <level.txt> [--seed N]         print the starting board for a seed
  m3lab tune <level.txt> [--band L:H] [--write]
                                            find the move budget that puts the greedy win rate in the band

options
  --runs N        games per level (default 1000; tune uses 400)
  --bot NAME      greedy | random | both (default both)
  --seed N        first seed (default 1)
  --threads N     parallelism (default: all cores)
  --csv FILE      also write the curve as CSV
  --band L:H      target greedy win-rate band for tune, e.g. 0.45:0.75 (default)
  --write         tune: rewrite the level file's moves line with the recommendation
";

        private static int Main(string[] args)
        {
            if (args.Length == 0) { Console.Write(Usage); return 1; }
            try
            {
                var opts = ParseOptions(args.Skip(2).ToArray());
                switch (args[0])
                {
                    case "validate": return Validate(args.ElementAtOrDefault(1));
                    case "sim": return Sim(args.ElementAtOrDefault(1), opts);
                    case "curve": return Curve(args.ElementAtOrDefault(1), opts);
                    case "check": return Check(args.ElementAtOrDefault(1), opts);
                    case "show": return Show(args.ElementAtOrDefault(1), opts);
                    case "tune": return Tune(args.ElementAtOrDefault(1), opts);
                    default: Console.Write(Usage); return 1;
                }
            }
            catch (LevelFormatException ex)
            {
                Console.Error.WriteLine("level error: " + ex.Message);
                return 2;
            }
            catch (FileNotFoundException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 2;
            }
        }

        private sealed class Options
        {
            public int Runs = 1000;
            public bool RunsGiven;
            public string Bot = "both";
            public ulong Seed = 1;
            public int Threads = Environment.ProcessorCount;
            public string Csv;
            public double BandLow = 0.45;
            public double BandHigh = 0.75;
            public bool Write;
        }

        private static Options ParseOptions(string[] args)
        {
            var o = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                string next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException("missing value after " + args[i]);
                switch (args[i])
                {
                    case "--runs": o.Runs = int.Parse(next(), CultureInfo.InvariantCulture); o.RunsGiven = true; break;
                    case "--band":
                    {
                        var parts = next().Split(':');
                        if (parts.Length != 2) throw new ArgumentException("--band expects L:H, e.g. 0.45:0.75");
                        o.BandLow = double.Parse(parts[0], CultureInfo.InvariantCulture);
                        o.BandHigh = double.Parse(parts[1], CultureInfo.InvariantCulture);
                        break;
                    }
                    case "--write": o.Write = true; break;
                    case "--bot": o.Bot = next(); break;
                    case "--seed": o.Seed = ulong.Parse(next(), CultureInfo.InvariantCulture); break;
                    case "--threads": o.Threads = int.Parse(next(), CultureInfo.InvariantCulture); break;
                    case "--csv": o.Csv = next(); break;
                    default: throw new ArgumentException("unknown option " + args[i]);
                }
            }
            return o;
        }

        private static IEnumerable<(string path, IBot bot)> Bots(string which)
        {
            if (which == "greedy" || which == "both") yield return ("greedy", new GreedyBot());
            if (which == "random" || which == "both") yield return ("random", new RandomBot());
        }

        private static Func<IBot> Factory(string name) => name == "random" ? new Func<IBot>(() => new RandomBot()) : () => new GreedyBot();

        private static List<string> LevelFiles(string pathOrDir)
        {
            if (pathOrDir == null) throw new ArgumentException("path required");
            if (Directory.Exists(pathOrDir))
                return Directory.GetFiles(pathOrDir, "*.txt").OrderBy(p => p, StringComparer.Ordinal).ToList();
            if (!File.Exists(pathOrDir)) throw new FileNotFoundException("no such file: " + pathOrDir);
            return new List<string> { pathOrDir };
        }

        private static int Validate(string path)
        {
            int bad = 0;
            foreach (var file in LevelFiles(path))
            {
                try
                {
                    var level = LevelText.Parse(File.ReadAllText(file));
                    Console.WriteLine("ok    " + Path.GetFileName(file) + "  " + level.Width + "x" + level.Height + ", " + level.Moves + " moves, " + level.Goals.Count + " goals");
                }
                catch (LevelFormatException ex)
                {
                    bad++;
                    Console.WriteLine("FAIL  " + Path.GetFileName(file) + "  " + ex.Message);
                }
            }
            return bad == 0 ? 0 : 2;
        }

        private static int Show(string path, Options o)
        {
            var level = LevelText.Parse(File.ReadAllText(LevelFiles(path)[0]));
            var game = Game.Start(level, o.Seed);
            Console.WriteLine(level.Name + "  seed " + o.Seed + "  moves " + level.Moves + "  goals: " + string.Join(", ", level.Goals));
            Console.Write(game.Board.Dump());
            Console.WriteLine(game.LegalMoves().Count + " legal moves" + (game.ShuffleCount > 0 ? " (after shuffle)" : ""));
            return 0;
        }

        private static int Sim(string path, Options o)
        {
            var level = LevelText.Parse(File.ReadAllText(LevelFiles(path)[0]));
            foreach (var (name, _) in Bots(o.Bot))
            {
                var result = Simulator.Run(level, Factory(name), new SimulationOptions { Runs = o.Runs, FirstSeed = o.Seed, MaxDegreeOfParallelism = o.Threads });
                Console.Write(result.Summary());
                Console.WriteLine();
            }
            return 0;
        }

        private static int Curve(string dir, Options o)
        {
            var files = LevelFiles(dir);
            var rows = new List<(string file, string bot, SimulationResult r)>();
            foreach (var file in files)
            {
                var level = LevelText.Parse(File.ReadAllText(file));
                foreach (var (name, _) in Bots(o.Bot))
                {
                    var r = Simulator.Run(level, Factory(name), new SimulationOptions { Runs = o.Runs, FirstSeed = o.Seed, MaxDegreeOfParallelism = o.Threads });
                    rows.Add((Path.GetFileNameWithoutExtension(file), name, r));
                    Console.Error.Write(".");
                }
            }
            Console.Error.WriteLine();

            Console.WriteLine(Pad("level", 22) + Pad("bot", 8) + Pad("win%", 8) + Pad("±", 6) + Pad("left", 6) + Pad("casc", 6) + Pad("shuf", 6) + "runs/s");
            foreach (var (file, bot, r) in rows)
            {
                Console.WriteLine(Pad(file, 22) + Pad(bot, 8) + Pad(F(r.WinRate * 100, 1), 8) + Pad(F(r.WinRateStandardError * 100, 1), 6)
                                  + Pad(F(r.AverageMovesLeftWhenWon, 1), 6) + Pad(F(r.AverageCascadesPerMove, 2), 6) + Pad(r.TotalShuffles.ToString(), 6) + F(r.RunsPerSecond, 0));
            }

            if (o.Csv != null)
            {
                var sb = new StringBuilder("level,bot,runs,win_rate,win_rate_se,avg_moves_left_when_won,avg_cascades_per_move,shuffles,avg_score,runs_per_second\n");
                foreach (var (file, bot, r) in rows)
                    sb.Append(string.Join(",", file, bot, r.Runs, F(r.WinRate, 4), F(r.WinRateStandardError, 4), F(r.AverageMovesLeftWhenWon, 3),
                        F(r.AverageCascadesPerMove, 3), r.TotalShuffles, F(r.AverageScore, 1), F(r.RunsPerSecond, 1))).Append('\n');
                File.WriteAllText(o.Csv, sb.ToString());
                Console.WriteLine("wrote " + o.Csv);
            }
            return 0;
        }

        private static int Check(string path, Options o)
        {
            // Fixed seeds make the verdict reproducible: same level and same rules give the same result in CI and locally.
            int bad = 0;
            foreach (var file in LevelFiles(path))
            {
                string label = Path.GetFileName(file);
                var level = LevelText.Parse(File.ReadAllText(file));
                if (!level.HasBand)
                {
                    bad++;
                    Console.WriteLine("FAIL  " + label + "  no 'band <low> <high>' line — declare the greedy win rate the level is meant to have");
                    continue;
                }
                var r = Simulator.Run(level, () => new GreedyBot(), new SimulationOptions { Runs = o.Runs, FirstSeed = o.Seed, MaxDegreeOfParallelism = o.Threads });
                double win = r.WinRate * 100;
                bool inBand = win >= level.BandLow && win <= level.BandHigh;
                if (!inBand) bad++;
                Console.WriteLine((inBand ? "ok    " : "FAIL  ") + label + "  greedy " + F(win, 1) + "% over " + r.Runs + " runs, band " + level.BandLow + "-" + level.BandHigh + "%"
                                  + (inBand ? "" : win < level.BandLow ? " — too hard: add moves or remove obstacles" : " — too easy: cut moves or add obstacles"));
            }
            return bad == 0 ? 0 : 3;
        }

        private static int Tune(string path, Options o)
        {
            string file = LevelFiles(path)[0];
            string text = File.ReadAllText(file);
            var level = LevelText.Parse(text);
            var options = new SimulationOptions { Runs = o.RunsGiven ? o.Runs : 400, FirstSeed = o.Seed, MaxDegreeOfParallelism = o.Threads };
            var result = MoveBudgetTuner.Tune(level, o.BandLow, o.BandHigh, () => new GreedyBot(), options);

            Console.WriteLine(level.Name + " — band " + F(o.BandLow * 100, 0) + "–" + F(o.BandHigh * 100, 0) + "% (greedy, " + options.Runs + " runs per step)");
            Console.WriteLine(result.Summary());

            if (o.Write && result.InBand && result.RecommendedMoves != level.Moves)
            {
                // Replace only the moves line so comments and formatting survive.
                var lines = text.Replace("\r\n", "\n").Split('\n');
                for (int i = 0; i < lines.Length; i++)
                    if (lines[i].TrimStart().StartsWith("moves ", StringComparison.OrdinalIgnoreCase))
                        lines[i] = "moves " + result.RecommendedMoves;
                File.WriteAllText(file, string.Join("\n", lines));
                Console.WriteLine("wrote moves " + result.RecommendedMoves + " to " + Path.GetFileName(file));
            }
            return result.InBand ? 0 : 3;
        }

        private static string F(double v, int digits) => v.ToString("F" + digits, CultureInfo.InvariantCulture);
        private static string Pad(string s, int width) => s.Length >= width ? s + " " : s.PadRight(width);
    }
}
