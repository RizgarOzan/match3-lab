using System.Linq;
using Match3Lab.Core;
using Match3Lab.Core.Simulation;
using Xunit;

namespace Match3Lab.Core.Tests
{
    public class EvolverTests
    {
        // A level the greedy bot wins almost always: generous budget, one small colour goal, a little grass.
        private static LevelDefinition EasyLevel()
        {
            return LevelText.Parse("name easy\nsize 7 7\nmoves 20\ncolors 4\ngoal color 0 12\ngoal grass 4\ngrid\n" +
                                   ". . . . . . .\n. . . . . . .\n. . .g .g . . .\n. . .g .g . . .\n. . . . . . .\n. . . . . . .\n. . . . . . .\n");
        }

        private static readonly SimulationOptions Quick = new SimulationOptions { Runs = 150 };

        [Fact]
        public void Evolver_makes_a_too_easy_level_harder_until_it_lands_in_the_band()
        {
            var level = EasyLevel();
            var before = Simulator.Run(level, () => new GreedyBot(), Quick).WinRate;
            Assert.True(before > 0.85, "precondition: the sample level should be easy, got " + before);

            var result = LevelEvolver.Evolve(level, 0.40, 0.70, () => new GreedyBot(), Quick, maxSteps: 60, seed: 3);

            Assert.True(result.InBand, "did not reach the band:\n" + result.Summary());
            Assert.InRange(result.WinRate, 0.40, 0.70);
            Assert.True(result.Accepted > 0);
            Assert.Empty(result.Level.Validate());
            Assert.Equal(level.Moves, result.Level.Moves); // the evolver never touches the budget
            Assert.Equal(level.Width, result.Level.Width);
            Assert.Equal(level.Height, result.Level.Height);
        }

        [Fact]
        public void Evolver_leaves_the_input_untouched_and_is_deterministic_for_a_seed()
        {
            var level = EasyLevel();
            string originalText = LevelText.Write(level);

            var a = LevelEvolver.Evolve(level, 0.40, 0.70, () => new GreedyBot(), Quick, maxSteps: 8, seed: 11);
            var b = LevelEvolver.Evolve(level, 0.40, 0.70, () => new GreedyBot(), Quick, maxSteps: 8, seed: 11);

            Assert.Equal(originalText, LevelText.Write(level));
            Assert.Equal(LevelText.Write(a.Level), LevelText.Write(b.Level));
            Assert.Equal(a.Steps.Select(s => s.ToString()), b.Steps.Select(s => s.ToString()));
        }

        [Fact]
        public void Evolver_returns_immediately_when_the_level_is_already_in_band()
        {
            var level = EasyLevel();
            var result = LevelEvolver.Evolve(level, 0.0, 1.0, () => new RandomBot(), Quick, maxSteps: 10, seed: 1);
            Assert.True(result.InBand);
            Assert.Empty(result.Steps);
            Assert.Same(level, result.Level);
        }

        [Fact]
        public void Every_mutation_is_valid_and_moves_exactly_one_dial()
        {
            var level = EasyLevel();
            var rng = new Pcg32(5);
            int produced = 0;
            for (int i = 0; i < 40; i++)
            {
                var mutated = LevelEvolver.Mutate(level, harder: i % 2 == 0, rng, out string what);
                if (mutated == null) continue;
                produced++;
                Assert.False(string.IsNullOrEmpty(what));
                Assert.Empty(mutated.Validate());
                Assert.NotEqual(LevelText.Write(level), LevelText.Write(mutated));
            }
            Assert.True(produced >= 30, "only " + produced + " of 40 attempts produced a mutation");
        }
    }
}
