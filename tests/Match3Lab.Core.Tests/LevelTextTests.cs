using Match3Lab.Core;
using Xunit;

namespace Match3Lab.Core.Tests
{
    public class LevelTextTests
    {
        private const string Sample = @"
# A small level with every cell kind in it.
name Sample One
size 5 4
moves 12
colors 4
goal color 2 10
goal grass 3
goal box 3
goal ice 2

grid
. . # . .
. .g 3i . b
.G . . .I B
- . .gi . .
";

        [Fact]
        public void Parses_header_goals_and_grid()
        {
            var level = LevelText.Parse(Sample);

            Assert.Equal("Sample One", level.Name);
            Assert.Equal(5, level.Width);
            Assert.Equal(4, level.Height);
            Assert.Equal(12, level.Moves);
            Assert.Equal(4, level.ColorCount);
            Assert.Equal(new[]
            {
                Goal.CollectColor(2, 10), Goal.ClearGrass(3), Goal.ClearBoxes(3), Goal.ClearIce(2),
            }, level.Goals);

            Assert.Equal(CellSpecKind.Hole, level.GetCell(2, 0).Kind);
            Assert.Equal(new CellSpec(CellSpecKind.Random, grass: 1), level.GetCell(1, 1));
            Assert.Equal(new CellSpec(CellSpecKind.FixedColor, color: 3, ice: 1), level.GetCell(2, 1));
            Assert.Equal(new CellSpec(CellSpecKind.Box, boxHitPoints: 1), level.GetCell(4, 1));
            Assert.Equal(new CellSpec(CellSpecKind.Random, grass: 2), level.GetCell(0, 2));
            Assert.Equal(new CellSpec(CellSpecKind.Random, ice: 2), level.GetCell(3, 2));
            Assert.Equal(new CellSpec(CellSpecKind.Box, boxHitPoints: 2), level.GetCell(4, 2));
            Assert.Equal(CellSpecKind.Empty, level.GetCell(0, 3).Kind);
            Assert.Equal(new CellSpec(CellSpecKind.Random, ice: 1, grass: 1), level.GetCell(2, 3));
        }

        [Fact]
        public void Write_then_parse_round_trips()
        {
            var level = LevelText.Parse(Sample);
            string written = LevelText.Write(level);
            var again = LevelText.Parse(written);

            Assert.Equal(level.Name, again.Name);
            Assert.Equal(level.Width, again.Width);
            Assert.Equal(level.Height, again.Height);
            Assert.Equal(level.Moves, again.Moves);
            Assert.Equal(level.ColorCount, again.ColorCount);
            Assert.Equal(level.Goals, again.Goals);
            Assert.Equal(level.Cells, again.Cells);
            Assert.Equal(written, LevelText.Write(again));
        }

        [Theory]
        [InlineData("size 3 3\nmoves 5\ncolors 3\ngoal grass 1\ngrid\n. . .\n. . .\n", "rows")]
        [InlineData("size 3 3\nmoves 5\ncolors 3\ngoal color 0 1\ngrid\n. . .\n. .\n. . .\n", "row 2")]
        [InlineData("size 3 3\nmoves 5\ncolors 3\ngoal color 0 1\ngrid\n. . .\n. x .\n. . .\n", "unknown cell token")]
        [InlineData("size 3 3\nmoves 5\ncolors 3\ngoal color 0 1\ngrid\n. . .\n. bi .\n. . .\n", "no modifiers")]
        [InlineData("size 3 3\nmoves 5\ncolors 3\ngoal color 5 1\ngrid\n. . .\n. . .\n. . .\n", "color 5")]
        [InlineData("size 3 3\nmoves 5\ncolors 3\ngoal grass 4\ngrid\n. . .\n.g . .\n. . .\n", "grass")]
        [InlineData("size 3 3\nmoves 0\ncolors 3\ngoal color 0 1\ngrid\n. . .\n. . .\n. . .\n", "moves")]
        [InlineData("moves 5\ncolors 3\ngoal color 0 1\ngrid\n. . .\n", "size")]
        [InlineData("size 3 3\nmoves 5\ncolors 3\ngoal color 0 1\nspeed 4\ngrid\n. . .\n. . .\n. . .\n", "unknown key")]
        public void Rejects_malformed_levels_with_a_reason(string text, string expectedFragment)
        {
            var ex = Assert.Throws<LevelFormatException>(() => LevelText.Parse(text));
            Assert.Contains(expectedFragment, ex.Message);
        }

        [Fact]
        public void Hole_tokens_are_not_mistaken_for_comments()
        {
            var level = LevelText.Parse("size 3 2\nmoves 5\ncolors 3\ngoal color 0 1\ngrid\n# . #\n. # .\n");
            Assert.Equal(CellSpecKind.Hole, level.GetCell(0, 0).Kind);
            Assert.Equal(CellSpecKind.Hole, level.GetCell(2, 0).Kind);
            Assert.Equal(CellSpecKind.Hole, level.GetCell(1, 1).Kind);
            Assert.Equal(CellSpecKind.Random, level.GetCell(1, 0).Kind);
        }
    }
}
