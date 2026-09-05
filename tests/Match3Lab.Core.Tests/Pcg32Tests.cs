using Match3Lab.Core;
using Xunit;

namespace Match3Lab.Core.Tests
{
    public class Pcg32Tests
    {
        [Fact]
        public void Matches_reference_implementation_for_seed_42_sequence_54()
        {
            // First six outputs of pcg32_srandom_r(&rng, 42u, 54u) from the pcg-c-basic demo.
            var rng = new Pcg32(42UL, 54UL);
            uint[] expected = { 0xa15c02b7u, 0x7b47f409u, 0xba1d3330u, 0x83d2f293u, 0xbfa4784bu, 0xcbed606eu };
            foreach (uint e in expected)
                Assert.Equal(e, rng.NextUInt());
        }

        [Fact]
        public void Same_seed_same_sequence_and_clone_is_independent()
        {
            var a = new Pcg32(7UL);
            var b = new Pcg32(7UL);
            for (int i = 0; i < 100; i++) Assert.Equal(a.NextUInt(), b.NextUInt());

            var c = a.Clone();
            uint fromA = a.NextUInt();
            uint fromC = c.NextUInt();
            Assert.Equal(fromA, fromC);
            a.NextUInt();
            Assert.NotEqual(a.NextUInt(), c.NextUInt());
        }

        [Fact]
        public void NextInt_stays_in_range_and_hits_every_value()
        {
            var rng = new Pcg32(1UL);
            var seen = new bool[6];
            for (int i = 0; i < 10_000; i++)
            {
                int v = rng.NextInt(6);
                Assert.InRange(v, 0, 5);
                seen[v] = true;
            }
            Assert.All(seen, s => Assert.True(s));
        }
    }
}
