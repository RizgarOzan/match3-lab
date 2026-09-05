# Match3 Lab

[![tests](https://github.com/RizgarOzan/match3-lab/actions/workflows/tests.yml/badge.svg)](https://github.com/RizgarOzan/match3-lab/actions/workflows/tests.yml)

A match-3 **workbench**, not a match-3 game. An engine-independent rules core, a level format
you can read in a diff, and a bot simulator that tells a level designer how hard a level is —
in seconds, before a human ever plays it.

> Status: **core, simulator and CLI done** (37 tests). Unity presentation, in-editor level
> editor and the WebGL demo are next — see [Roadmap](#roadmap).

## The question it answers

Casual studios tune every level by having bots play it thousands of times. That tooling is
in-house everywhere and open-source nowhere. This is the open one:

```
$ dotnet run --project tools/Match3Lab.Cli -- curve levels --runs 1000

level                 bot     win%    ±     left  casc  shuf  runs/s
01-first-steps        greedy  100.0   0.0   6.3   2.38  0     2033
01-first-steps        random  70.1    1.4   2.9   1.11  0     10638
02-mow-the-lawn       greedy  98.0    0.4   5.9   2.17  0     1592
02-mow-the-lawn       random  16.7    1.2   2.3   1.12  0     7874
03-crates             greedy  90.8    0.9   7.2   1.25  1     1718
03-crates             random  27.3    1.4   4.0   0.70  3     6098
04-thin-ice           greedy  80.0    1.3   5.3   0.62  242   2041
04-thin-ice           random  29.7    1.4   3.2   0.41  374   5525
05-hourglass          greedy  59.7    1.6   3.9   0.75  49    1786
05-hourglass          random  9.1     0.9   2.5   0.47  73    5988
06-cold-storage       greedy  49.7    1.6   4.3   0.77  72    1570
06-cold-storage       random  3.1     0.5   3.2   0.48  123   5650
```

12,000 games in about five seconds on a laptop (AMD Ryzen 9 270, all cores). Columns: win rate
with its standard error, average moves left when won (slack), cascades per move, how many of the
1000 games dead-ended into a shuffle, and throughput.

**How the six sample levels got their numbers.** They were written by hand in
[`levels/`](levels/). The validator rejected two of them on the first try (goal counts that the
layout could not satisfy). The first curve came back 100 / 100 / 99 / 99 / 94 / 85 % — which
turned out to be a bug in the *bot*, not the levels: it was cloning the game's RNG and choosing
moves with knowledge of which pieces would fall next. With that fixed
([decision 0003](docs/decisions/0003-bots-and-the-foresight-bug.md)) the hard levels dropped by
up to 20 points, and one pass of move-budget changes produced the curve above. Each iteration
took about five seconds.

## What is in the box

```
packages/com.rizgarozan.match3lab.core/   rules core — pure C#, no UnityEngine (a Unity package; the single source of truth)
  Runtime/                                board, matching, specials, gravity, goals, level text format
  Runtime/Simulation/                     bots and the parallel simulator
src/Match3Lab.Core/                       .NET project that compiles the same files for tests and tools
tests/Match3Lab.Core.Tests/               xUnit — 37 tests pin the rules, determinism and the simulator
tools/Match3Lab.Cli/                      m3lab: validate · show · sim · curve (CSV out)
levels/                                   six hand-authored levels, easy to hard
docs/decisions/                           why things are the way they are (ADRs)
unity/                                    Unity 6 project — presentation, editor window, WebGL (in progress)
```

### The rules core

- **Deterministic by construction.** Same level + same seed = same game, on Mono, IL2CPP and
  .NET. Randomness is an in-repo PCG32 verified against the reference outputs; `System.Random`
  is never used. ([decision 0001](docs/decisions/0001-pure-core-and-determinism.md))
- **Events, not callbacks.** `Game.Play(move)` returns the full list of what happened — swaps,
  clears, obstacle hits, specials created and fired, falls, spawns — grouped by phase. The
  presentation layer replays it; it never computes a rule.
- **Royal-Match-shaped rule set:** rockets, bombs, rainbows and their six combinations; grass,
  ice, boxes and holes; tap-to-fire. Every rule and its reason is in
  [decision 0002](docs/decisions/0002-resolution-rules.md).
- **Bots see what a player sees.** Look-ahead clones use an imagined refill stream, never the
  real one.

### The level format

```
name 05 Hourglass
size 7 9
moves 18
colors 5
goal color 2 24
goal grass 6
grid
. . . . . . .
. . . . . . .
# . . . . . #
# # .g .g .g # #
# # # . # # #
# # .g .g .g # #
# . . . . . #
. . . . . . .
. . . . . . .
```

`.` random piece · `0-5` fixed colour · `#` hole · `b`/`B` box with 1/2 hit points ·
`g`/`G` grass under · `i`/`I` ice over. It round-trips through the parser and the writer, and
the validator explains exactly what is wrong when something is.

## Try it

```
dotnet test tests/Match3Lab.Core.Tests
dotnet run --project tools/Match3Lab.Cli -- validate levels
dotnet run --project tools/Match3Lab.Cli -- show levels/05-hourglass.txt --seed 7
dotnet run --project tools/Match3Lab.Cli -- sim levels/06-cold-storage.txt --runs 2000
dotnet run --project tools/Match3Lab.Cli -- curve levels --runs 1000 --csv curve.csv
```

Needs the .NET 10 SDK. The Unity side needs Unity 6000.3.

## Roadmap

1. **Unity presentation** — playable board driven purely by the event stream, pooled sprites,
   WebGL build on itch.io.
2. **Level editor window** — paint tiles, set goals, validate, save to the text format.
3. **Simulator window** — run the curve from inside the editor, chart it, flag levels outside
   the target win-rate band.
4. **Level suggestion loop** — propose a level, let the bots validate it, keep it only if it
   lands in the band.

## License

MIT — see [LICENSE](LICENSE).
