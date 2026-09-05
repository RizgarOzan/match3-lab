using System.Linq;
using Match3Lab.Core;
using Match3Lab.Core.Simulation;
using Xunit;

namespace Match3Lab.Core.Tests
{
    public class SimulatorTests
    {
        private static LevelDefinition SmallLevel(int moves = 12, string goals = "goal color 0 18")
        {
            return LevelText.Parse("name sim\nsize 6 6\nmoves " + moves + "\ncolors 4\n" + goals + "\ngrid\n" +
                                   string.Concat(Enumerable.Repeat(". . . . . .\n", 6)));
        }

        [Fact]
        public void Results_are_identical_across_parallel_and_sequential_runs()
        {
            var level = SmallLevel();
            var seq = Simulator.Run(level, () => new GreedyBot(), new SimulationOptions { Runs = 60, MaxDegreeOfParallelism = 1 });
            var par = Simulator.Run(level, () => new GreedyBot(), new SimulationOptions { Runs = 60, MaxDegreeOfParallelism = 4 });

            Assert.Equal(seq.Wins, par.Wins);
            Assert.Equal(seq.TotalMovesPlayed, par.TotalMovesPlayed);
            Assert.Equal(seq.TotalCascades, par.TotalCascades);
            Assert.Equal(seq.TotalScore, par.TotalScore);
            Assert.Equal(seq.MovesLeftWhenWon, par.MovesLeftWhenWon);
            Assert.Equal(seq.GoalFractionBasisPoints, par.GoalFractionBasisPoints);
        }

        [Fact]
        public void Every_run_ends_in_a_terminal_state_and_counts_add_up()
        {
            var result = Simulator.Run(SmallLevel(), () => new RandomBot(), new SimulationOptions { Runs = 100 });

            Assert.Equal(100, result.Wins + result.Losses);
            Assert.Equal(result.Wins, result.MovesLeftWhenWon.Sum());
            Assert.InRange(result.WinRate, 0.0, 1.0);
            Assert.True(result.ElapsedMilliseconds >= 0);
        }

        [Fact]
        public void Greedy_wins_more_often_than_random()
        {
            var level = SmallLevel(moves: 10, goals: "goal color 0 20");
            var options = new SimulationOptions { Runs = 200 };
            var random = Simulator.Run(level, () => new RandomBot(), options);
            var greedy = Simulator.Run(level, () => new GreedyBot(), options);

            Assert.True(greedy.WinRate > random.WinRate,
                "greedy " + greedy.WinRate + " should beat random " + random.WinRate);
        }

        [Fact]
        public void Tuner_finds_a_move_budget_inside_a_wide_band()
        {
            var level = SmallLevel(moves: 30, goals: "goal color 0 24");
            var result = MoveBudgetTuner.Tune(level, 0.30, 0.90, () => new GreedyBot(), new SimulationOptions { Runs = 150 });

            Assert.True(result.InBand, result.Summary());
            Assert.InRange(result.WinRate, 0.30, 0.90);
            Assert.True(result.RecommendedMoves < 30, "30 moves should be too easy: " + result.Summary());
            Assert.True(result.Steps.Count <= 9, "binary search should need few steps, took " + result.Steps.Count);
        }

        [Fact]
        public void Tuner_reports_when_no_budget_can_reach_the_band()
        {
            var level = SmallLevel(moves: 5, goals: "goal color 0 5000");
            var result = MoveBudgetTuner.Tune(level, 0.30, 0.90, () => new GreedyBot(), new SimulationOptions { Runs = 20 }, maxMoves: 10);

            Assert.False(result.InBand);
            Assert.Contains("layout", result.Note);
        }

        [Fact]
        public void A_level_with_an_impossible_goal_has_zero_win_rate()
        {
            var level = SmallLevel(moves: 2, goals: "goal color 0 500");
            var result = Simulator.Run(level, () => new GreedyBot(), new SimulationOptions { Runs = 20 });

            Assert.Equal(0, result.Wins);
            Assert.Equal(0.0, result.WinRate);
            Assert.Equal(0.0, result.GoalCompletionRate(0));
        }
    }
}
